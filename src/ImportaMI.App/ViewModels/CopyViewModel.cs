using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportaMI.App.Localization;
using ImportaMI.App.Resources;
using ImportaMI.App.Services;
using ImportaMI.Core.Import;

namespace ImportaMI.App.ViewModels;

/// <summary>Una sessione durante la copia e nel report.</summary>
public partial class CopySessionRow(PlannedSession session, long startByte) : ObservableObject
{
    public PlannedSession Session { get; } = session;

    public long StartByte { get; } = startByte;

    public string Name => Session.Name;

    public string Folder => Session.FolderPath;

    public string SizeText { get; } = Format.Size(session.Bytes);

    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = Strings.StatusWaiting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsOk), nameof(IsError))]
    public partial string StatusKind { get; set; } = "waiting";

    public bool IsRunning => StatusKind == "running";

    public bool IsOk => StatusKind == "done";

    public bool IsError => StatusKind == "error";

    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;
}

/// <summary>Un file non copiato, con il motivo nella lingua dell'interfaccia.</summary>
public sealed record FailureItem(string FileName, string Reason, string? Detail);

/// <summary>Passaggio 4: copia con avanzamento, poi report.</summary>
public partial class CopyViewModel : ViewModelBase
{
    private readonly ImportOptions _options;
    private readonly SourceInfo _source;
    private readonly Action _goToCleanup;
    private readonly Action _newImport;
    private readonly Func<string, Task<bool>> _eject;
    private readonly PauseGate _pause = new();
    private readonly string _reportsFolder;
    private readonly Stopwatch _clock = new();
    private CancellationTokenSource? _cancel;
    private long _bytesAtLastSample;
    private TimeSpan _timeAtLastSample;
    private double _speed;

    public CopyViewModel(
        ImportPlan plan,
        ImportOptions options,
        SourceInfo source,
        bool proposeCleanup,
        bool ejectWhenDone,
        Action goToCleanup,
        Action newImport,
        Func<string, Task<bool>> eject,
        string reportsFolder)
    {
        _reportsFolder = reportsFolder;
        _options = options;
        _source = source;
        _goToCleanup = goToCleanup;
        _newImport = newImport;
        _eject = eject;
        ProposeCleanup = proposeCleanup;
        EjectWhenDone = ejectWhenDone;
        Plan = plan;

        long start = 0;
        foreach (var session in plan.Sessions)
        {
            Rows.Add(new CopySessionRow(session, start));
            start += session.Bytes;
        }

        Subtitle = Format.Text(Strings.CopySubtitle, source.Label, options.DestinationRoot);
    }

    public ImportPlan Plan { get; }

    public bool ProposeCleanup { get; }

    public bool EjectWhenDone { get; }

    public string Subtitle { get; }

    public ObservableCollection<CopySessionRow> Rows { get; } = [];

    public ObservableCollection<FailureItem> Failures { get; } = [];

    public ImportReport? Report { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand), nameof(CleanupCommand), nameof(NewImportCommand))]
    public partial bool IsFinished { get; set; }

    public bool IsRunning => !IsFinished;

    [ObservableProperty]
    public partial string Title { get; set; } = Strings.CopyTitle;

    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial string PercentText { get; set; } = "0%";

    [ObservableProperty]
    public partial string StatsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseLabel))]
    public partial bool IsPaused { get; set; }

    public string PauseLabel => IsPaused ? Strings.Resume : Strings.Pause;

    // Report
    [ObservableProperty]
    public partial string CopiedValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CopiedDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SkippedValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FailedValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFailures { get; set; }

    [ObservableProperty]
    public partial string DurationValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DurationDetail { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStopMessage))]
    public partial string? StopMessage { get; set; }

    public bool HasStopMessage => StopMessage is not null;

    [ObservableProperty]
    public partial string? ReportPath { get; set; }

    [ObservableProperty]
    public partial string? ReportSavedText { get; set; }

    [ObservableProperty]
    public partial string RetryLabel { get; set; } = string.Empty;

    /// <summary>Ci sono file falliti o non raggiunti da riprovare.</summary>
    [ObservableProperty]
    public partial bool ShowRetry { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEjectMessage))]
    public partial string? EjectMessage { get; set; }

    public bool HasEjectMessage => EjectMessage is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EjectCommand))]
    public partial bool CanEject { get; set; }

    /// <summary>Avvia la copia. La view-model resta in vita anche se l'utente cambia passaggio.</summary>
    public Task StartAsync() => RunAsync((runner, progress, token) => runner.RunAsync(Plan, _options, progress, _pause, token));

    [RelayCommand]
    private void TogglePause()
    {
        if (_pause.IsPaused)
            _pause.Resume();
        else
            _pause.Pause();
        IsPaused = _pause.IsPaused;
    }

    [RelayCommand]
    private void Cancel()
    {
        _pause.Resume();
        IsPaused = false;
        _cancel?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private Task RetryAsync()
    {
        var previous = Report!;
        return RunAsync((runner, progress, token) => runner.RetryAsync(previous, _options, progress, _pause, token));
    }

    private bool CanRetry() => IsFinished && Report?.Retryable.Count > 0;

    [RelayCommand]
    private void OpenFolder() => Shell.Open(_options.DestinationRoot);

    [RelayCommand]
    private void OpenReport()
    {
        if (ReportPath is not null)
            Shell.Open(ReportPath);
    }

    [RelayCommand(CanExecute = nameof(CanCleanup))]
    private void Cleanup() => _goToCleanup();

    private bool CanCleanup() => IsFinished && Report?.Results.Any(r => r.IsSafe) == true;

    [RelayCommand(CanExecute = nameof(IsFinished))]
    private void NewImport() => _newImport();

    [RelayCommand(CanExecute = nameof(CanEject))]
    private async Task EjectAsync()
    {
        CanEject = false;
        var ejected = await _eject(_source.Root);
        EjectMessage = Format.Text(ejected ? Strings.Ejected : Strings.EjectFailed, _source.Label);
        CanEject = !ejected;
    }

    private async Task RunAsync(Func<ImportRunner, IProgress<CopyProgress>, CancellationToken, Task<ImportReport>> run)
    {
        IsFinished = false;
        Title = Strings.CopyTitle;
        StopMessage = null;
        _cancel = new CancellationTokenSource();
        _clock.Restart();
        _bytesAtLastSample = 0;
        _timeAtLastSample = TimeSpan.Zero;
        _speed = 0;

        // Creato qui, sul thread dell'interfaccia, così gli aggiornamenti tornano su questo thread.
        var progress = new Progress<CopyProgress>(OnProgress);
        var token = _cancel.Token;
        try
        {
            Report = await Task.Run(() => run(new ImportRunner(), progress, token));
        }
        catch (Exception e)
        {
            // Ultima rete di sicurezza: un errore imprevisto non deve chiudere l'app né perdere il report già fatto.
            AppLog.Error("Copia interrotta da un errore imprevisto", e);
            _clock.Stop();
            Title = Strings.ReportStoppedTitle;
            StopMessage = Format.Text(Strings.ScanFailedDetail, e.Message);
            IsFinished = true;
            return;
        }
        finally
        {
            _cancel.Dispose();
            _cancel = null;
        }

        _clock.Stop();
        ShowReport(Report);
    }

    private void OnProgress(CopyProgress p)
    {
        // Aggiornamenti arrivati in ritardo dopo la fine non devono sovrascrivere il report.
        if (IsFinished)
            return;

        // Velocità su finestre di almeno un secondo, così il numero non salta a ogni blocco.
        var elapsed = _clock.Elapsed;
        if (elapsed - _timeAtLastSample >= TimeSpan.FromSeconds(1))
        {
            var bytesPerSecond = (p.BytesDone - _bytesAtLastSample) / (elapsed - _timeAtLastSample).TotalSeconds;
            _speed = _speed == 0 ? bytesPerSecond : (_speed * 0.6) + (bytesPerSecond * 0.4);
            _bytesAtLastSample = p.BytesDone;
            _timeAtLastSample = elapsed;
        }

        Percent = p.TotalBytes == 0 ? 100 : 100.0 * p.BytesDone / p.TotalBytes;
        PercentText = $"{Math.Floor(Percent):0}%";
        var remaining = _speed > 0 ? TimeSpan.FromSeconds((p.TotalBytes - p.BytesDone) / _speed) : (TimeSpan?)null;
        StatsText = IsPaused
            ? Strings.CopyPaused
            : Format.Text(Strings.CopyStats, Format.Size(p.BytesDone), Format.Size(p.TotalBytes),
                _speed > 0 ? Format.Size((long)_speed) : "…", remaining is { } r ? Format.Duration(r) : "…");
        CurrentText = p.Verifying ? Format.Text(Strings.CopyVerifying, p.CurrentFile) : p.CurrentFile;

        // Avanzamento per sessione, dai byte già copiati: i file del piano sono ordinati per sessione.
        foreach (var row in Rows)
        {
            if (row.Session.Bytes == 0)
                continue;
            var done = Math.Clamp(p.BytesDone - row.StartByte, 0, row.Session.Bytes);
            row.Percent = 100.0 * done / row.Session.Bytes;
            (row.Status, row.StatusKind) = done == 0 ? (Strings.StatusWaiting, "waiting")
                : done < row.Session.Bytes ? (Strings.StatusCopying, "running")
                : (Strings.StatusDone, "done");
        }
    }

    private void ShowReport(ImportReport report)
    {
        var copied = report.Count(FileOutcome.Copied);
        var skipped = report.Count(FileOutcome.SkippedIdentical);
        var failed = report.Count(FileOutcome.Failed) + report.Count(FileOutcome.NotProcessed);
        var duration = report.Finished - report.Started;

        Title = report.StopReason == StopReason.None ? Strings.ReportDoneTitle : Strings.ReportStoppedTitle;
        CopiedValue = Format.Number(copied);
        CopiedDetail = Format.Size(report.Bytes(FileOutcome.Copied)) + (_options.Verify && copied > 0 ? " · " + Strings.TileVerified : string.Empty);
        SkippedValue = Format.Number(skipped);
        FailedValue = Format.Number(failed);
        HasFailures = failed > 0;
        DurationValue = Format.Duration(duration);
        DurationDetail = duration.TotalSeconds >= 1
            ? Format.Text(Strings.TileSpeed, Format.Size((long)(report.Bytes(FileOutcome.Copied) / duration.TotalSeconds)))
            : string.Empty;
        StopMessage = report.StopReason switch
        {
            StopReason.Canceled => Strings.StopCanceled,
            StopReason.SourceDisconnected => Strings.StopSourceDisconnected,
            StopReason.DestinationFull => Strings.StopDestinationFull,
            StopReason.DestinationUnavailable => Strings.StopDestinationUnavailable,
            _ => null,
        };
        RetryLabel = Format.Text(Strings.RetryFiles, Format.Number(report.Retryable.Count));
        ShowRetry = report.Retryable.Count > 0;

        foreach (var row in Rows)
        {
            var results = report.ForSession(row.Session.Index).ToList();
            var ok = results.Count(r => r.Outcome == FileOutcome.Copied);
            var same = results.Count(r => r.Outcome == FileOutcome.SkippedIdentical);
            var bad = results.Count(r => !r.IsSafe);
            row.Percent = 100;
            (row.Status, row.StatusKind) = bad > 0
                ? (report.StopReason == StopReason.None ? Strings.StatusErrors : Strings.StatusStopped, "error")
                : (_options.Verify ? Strings.StatusVerified : Strings.StatusDone, "done");
            row.ResultText = Format.Text(Strings.SessionResult, Format.Number(ok), Format.Number(same), Format.Number(bad));
        }

        Failures.Clear();
        foreach (var result in report.Results.Where(r => r.Outcome == FileOutcome.Failed))
        {
            var reason = result.Failure switch
            {
                FileFailure.VerificationMismatch => Strings.FailureVerification,
                FileFailure.SourceMissing => Strings.FailureSourceMissing,
                _ => Strings.FailureReadOrWrite,
            };
            Failures.Add(new FailureItem(result.File.Source.FileName, reason, result.Error));
        }

        try
        {
            ReportPath = ReportWriter.Save(report, _reportsFolder);
            ReportSavedText = Format.Text(Strings.ReportSaved, ReportPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Report non salvato", e);
            ReportPath = null;
            ReportSavedText = null;
        }

        AppLog.Info($"Copia finita in {duration}: {copied} copiati, {skipped} già presenti, {failed} non copiati, " +
                    $"esito {report.StopReason}, report {ReportPath ?? "non salvato"}");
        foreach (var result in report.Results.Where(r => r.Outcome == FileOutcome.Failed))
            AppLog.Warning($"Non copiato {result.File.Source.SourcePath}: {result.Failure} {result.Error}");

        IsFinished = true;
        RetryCommand.NotifyCanExecuteChanged();
        CleanupCommand.NotifyCanExecuteChanged();

        // Senza pulizia proposta, la scheda si espelle subito dopo una copia completa.
        CanEject = _source.CanEject && !ProposeCleanup;
        if (CanEject && EjectWhenDone && report.StopReason == StopReason.None && failed == 0)
            EjectCommand.Execute(null);
    }
}

/// <summary>La sorgente scelta nel passaggio 1.</summary>
public sealed record SourceInfo(string Root, string Label, bool CanEject);
