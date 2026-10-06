using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportaMI.App.Localization;
using ImportaMI.App.Resources;
using ImportaMI.App.Services;
using ImportaMI.Core.Cleanup;
using ImportaMI.Core.Import;
using ImportaMI.Core.Scanning;

namespace ImportaMI.App.ViewModels;

/// <summary>Una sessione da (non) eliminare dalla scheda.</summary>
public partial class CleanupSessionRow : ObservableObject
{
    private readonly Action _changed;

    public CleanupSessionRow(PlannedSession session, IReadOnlyList<FileResult> deletable, int notCopied, Action changed)
    {
        _changed = changed;
        Session = session;
        Deletable = deletable;
        FilesText = Format.Text(Strings.CleanSessionFiles, Format.Number(deletable.Count), Format.Size(deletable.Sum(r => r.SourceSize)));
        NotCopiedText = notCopied > 0 ? Format.Text(Strings.CleanSessionFailed, Format.Number(notCopied)) : null;
        IsSelected = deletable.Count > 0;
    }

    public PlannedSession Session { get; }

    public IReadOnlyList<FileResult> Deletable { get; }

    public string Name => Session.Name;

    public string FilesText { get; }

    public string? NotCopiedText { get; }

    public bool HasNotCopied => NotCopiedText is not null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => _changed();
}

/// <summary>Una riga di "resta sulla scheda" o dei file tenuti dopo la pulizia.</summary>
public sealed record StayItem(string Title, string Detail);

/// <summary>Passaggio 5: pulizia facoltativa della scheda.</summary>
public partial class CleanupViewModel : ViewModelBase
{
    private readonly ImportReport _report;
    private readonly SourceInfo _source;
    private readonly bool _ejectWhenDone;
    private readonly Action _newImport;
    private readonly Func<string, Task<bool>> _eject;
    private readonly int _notImported;
    private readonly int _otherFiles;
    private CancellationTokenSource? _cancel;

    public CleanupViewModel(
        ImportReport report,
        ScanResult scan,
        SourceInfo source,
        bool ejectWhenDone,
        Action newImport,
        Func<string, Task<bool>> eject)
    {
        _report = report;
        _source = source;
        _ejectWhenDone = ejectWhenDone;
        _newImport = newImport;
        _eject = eject;

        var planned = report.Plan.Files.Select(f => f.Source.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _notImported = scan.Files.Count(f => !planned.Contains(f.SourcePath));
        _otherFiles = scan.OtherFiles;

        foreach (var session in report.Plan.Sessions)
        {
            var results = report.ForSession(session.Index).ToList();
            var deletable = CardCleaner.Candidates(report, new HashSet<int> { session.Index });
            Sessions.Add(new CleanupSessionRow(session, deletable, results.Count(r => !r.IsSafe), Refresh));
        }

        Subtitle = Format.Text(Strings.CleanSubtitle, source.Label);
        Refresh();
    }

    public string Subtitle { get; }

    public ObservableCollection<CleanupSessionRow> Sessions { get; } = [];

    public ObservableCollection<StayItem> Stays { get; } = [];

    public ObservableCollection<StayItem> Kept { get; } = [];

    /// <summary>"setup", "running" o "done": quale parte della schermata mostrare.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetup), nameof(IsRunning), nameof(IsDone))]
    public partial string Phase { get; set; } = "setup";

    public bool IsSetup => Phase == "setup";

    public bool IsRunning => Phase == "running";

    public bool IsDone => Phase == "done";

    [ObservableProperty]
    public partial string Title { get; set; } = Strings.CleanTitle;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial bool Acknowledged { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial int SelectedFiles { get; set; }

    [ObservableProperty]
    public partial string DeleteLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double NowPercent { get; set; }

    [ObservableProperty]
    public partial double AfterPercent { get; set; }

    [ObservableProperty]
    public partial double FreedPercent { get; set; }

    [ObservableProperty]
    public partial string NowText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AfterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSpace { get; set; }

    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CurrentFile { get; set; } = string.Empty;

    // Esito
    [ObservableProperty]
    public partial string DeletedValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FreedValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KeptValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string KeptDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FreeNowValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowTiles { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDoneMessage))]
    public partial string? DoneMessage { get; set; }

    public bool HasDoneMessage => DoneMessage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEjectMessage))]
    public partial string? EjectMessage { get; set; }

    public bool HasEjectMessage => EjectMessage is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EjectCommand))]
    public partial bool CanEject { get; set; }

    public bool HasKept => Kept.Count > 0;

    /// <summary>File eliminati dalla scheda (0 se la pulizia non è stata fatta).</summary>
    public int DeletedCount { get; private set; }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var items = Sessions.Where(s => s.IsSelected).SelectMany(s => s.Deletable).ToList();
        Phase = "running";
        Title = Strings.CleanRunning;
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        var progress = new Progress<CleanupProgress>(p =>
        {
            Percent = p.Total == 0 ? 100 : 100.0 * p.Done / p.Total;
            ProgressText = Format.Text(Strings.CleanProgress, Format.Number(p.Done), Format.Number(p.Total), Format.Size(p.FreedBytes));
            CurrentFile = p.CurrentFile;
        });

        CleanupReport result;
        try
        {
            result = await Task.Run(() => CardCleaner.RunAsync(_source.Root, items, progress, token));
        }
        catch (Exception e)
        {
            // Ultima rete di sicurezza: i file non ancora controllati restano sulla scheda.
            AppLog.Error("Pulizia interrotta da un errore imprevisto", e);
            Title = Strings.CleanStoppedTitle;
            DoneMessage = Format.Text(Strings.ScanFailedDetail, e.Message);
            ShowTiles = false;
            Phase = "done";
            return;
        }
        finally
        {
            _cancel.Dispose();
            _cancel = null;
        }

        await ShowResultAsync(result);
    }

    private bool CanDelete() => Acknowledged && SelectedFiles > 0;

    [RelayCommand]
    private void CancelCleanup() => _cancel?.Cancel();

    [RelayCommand]
    private async Task SkipAsync()
    {
        AppLog.Info($"Pulizia di {_source.Root} saltata");
        Title = Strings.CleanSkippedTitle;
        DoneMessage = Format.Text(Strings.CleanSkippedDetail, _source.Label);
        ShowTiles = false;
        Phase = "done";
        await AfterDoneAsync(ok: true);
    }

    [RelayCommand]
    private void OpenFolder() => Services.Shell.Open(_report.Plan.DestinationRoot);

    [RelayCommand]
    private void NewImport() => _newImport();

    [RelayCommand(CanExecute = nameof(CanEject))]
    private async Task EjectAsync()
    {
        CanEject = false;
        var ejected = await _eject(_source.Root);
        EjectMessage = Format.Text(ejected ? Strings.Ejected : Strings.EjectFailed, _source.Label);
        CanEject = !ejected;
    }

    private void Refresh()
    {
        var selected = Sessions.Where(s => s.IsSelected).ToList();
        SelectedFiles = selected.Sum(s => s.Deletable.Count);
        var bytes = selected.Sum(s => s.Deletable.Sum(r => r.SourceSize));
        DeleteLabel = Format.Text(Strings.CleanDelete, Format.Number(SelectedFiles), Format.Size(bytes));

        Stays.Clear();
        if (_notImported > 0)
            Stays.Add(new StayItem(Format.Text(Strings.CleanStayNotImported, Format.Number(_notImported)), Strings.CleanStayNotImportedDetail));
        var notCopied = _report.Results.Count(r => !r.IsSafe);
        if (notCopied > 0)
            Stays.Add(new StayItem(Format.Text(Strings.CleanStayFailed, Format.Number(notCopied)), Strings.CleanStayFailedDetail));
        var excluded = Sessions.Where(s => !s.IsSelected).Sum(s => s.Deletable.Count);
        if (excluded > 0)
            Stays.Add(new StayItem(Format.Text(Strings.CleanStayExcluded, Format.Number(excluded)), string.Join(", ", Sessions.Where(s => !s.IsSelected && s.Deletable.Count > 0).Select(s => s.Name))));
        if (_otherFiles > 0)
            Stays.Add(new StayItem(Format.Text(Strings.CleanStayOther, Format.Number(_otherFiles)), Strings.CleanStayOtherDetail));

        if (StorageSources.SpaceFor(_source.Root) is { } space && space.Total > 0)
        {
            HasSpace = true;
            var used = space.Total - space.Free;
            NowPercent = 100.0 * used / space.Total;
            AfterPercent = 100.0 * Math.Max(0, used - bytes) / space.Total;
            FreedPercent = NowPercent - AfterPercent;
            NowText = Format.Text(Strings.SourceSpace, Format.Size(used), Format.Size(space.Total));
            AfterText = Format.Text(Strings.SourceSpace, Format.Size(Math.Max(0, used - bytes)), Format.Size(space.Total));
        }
        else
        {
            HasSpace = false;
        }
    }

    private async Task ShowResultAsync(CleanupReport result)
    {
        Title = result.Stop == CleanupStop.None ? Strings.CleanDoneTitle : Strings.CleanStoppedTitle;
        DoneMessage = result.Stop switch
        {
            CleanupStop.Canceled => Strings.CleanStopCanceled,
            CleanupStop.SourceDisconnected => Strings.CleanStopSource,
            _ => null,
        };

        var kept = result.Kept.ToList();
        AppLog.Info($"Pulizia di {_source.Root}: {result.Deleted} eliminati, {result.FreedBytes:N0} byte liberati, " +
                    $"{kept.Count} tenuti, esito {result.Stop}");
        foreach (var item in kept)
            AppLog.Warning($"Tenuto sulla scheda {item.Item.File.Source.SourcePath}: {item.Outcome} {item.Error}");
        DeletedCount = result.Deleted;
        DeletedValue = Format.Number(result.Deleted);
        FreedValue = Format.Size(result.FreedBytes);
        KeptValue = Format.Number(kept.Count);
        KeptDetail = kept.Count > 0 ? Strings.TileKeptDetail : Strings.TileKeptNone;
        FreeNowValue = StorageSources.SpaceFor(_source.Root) is { } space ? Format.Size(space.Free) : "—";
        ShowTiles = true;

        Kept.Clear();
        foreach (var group in kept.GroupBy(k => k.Outcome))
        {
            var reason = group.Key switch
            {
                CleanupOutcome.KeptProtected => Strings.KeptProtected,
                CleanupOutcome.KeptSourceChanged => Strings.KeptSourceChanged,
                CleanupOutcome.KeptCopyMissing => Strings.KeptCopyMissing,
                CleanupOutcome.KeptCopyDifferent => Strings.KeptCopyDifferent,
                _ => Strings.KeptFailed,
            };
            Kept.Add(new StayItem($"{reason} ({Format.Number(group.Count())})", string.Join(", ", group.Take(8).Select(k => k.Item.File.Source.FileName)) + (group.Count() > 8 ? ", …" : string.Empty)));
        }
        OnPropertyChanged(nameof(HasKept));

        Phase = "done";
        await AfterDoneAsync(ok: result.Stop == CleanupStop.None);
    }

    private async Task AfterDoneAsync(bool ok)
    {
        CanEject = _source.CanEject;
        if (CanEject && _ejectWhenDone && ok)
            await EjectAsync();
    }
}
