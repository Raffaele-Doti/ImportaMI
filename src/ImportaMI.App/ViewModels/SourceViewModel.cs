using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportaMI.App.Localization;
using ImportaMI.App.Resources;
using ImportaMI.App.Services;
using ImportaMI.Core.Scanning;

namespace ImportaMI.App.ViewModels;

/// <summary>Una memoria o una cartella da cui importare.</summary>
public partial class SourceItemViewModel : ObservableObject
{
    public SourceItemViewModel(StorageSource source)
    {
        RootPath = source.RootPath;
        Label = source.Label;
        IsRemovable = source.IsRemovable;
        Description = $"{(source.IsRemovable ? Strings.SourceRemovable : Strings.SourceDrive)} · {source.RootPath}";
        var used = source.TotalBytes - source.FreeBytes;
        SpaceText = Format.Text(Strings.SourceSpace, Format.Size(used), Format.Size(source.TotalBytes));
        UsedPercent = source.TotalBytes > 0 ? 100.0 * used / source.TotalBytes : 0;
    }

    public SourceItemViewModel(string folder)
    {
        RootPath = folder;
        Label = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(Label))
            Label = folder;
        IsFolder = true;
        Description = $"{Strings.SourceFolder} · {folder}";
    }

    public string RootPath { get; }

    public string Label { get; }

    public string Description { get; }

    public string? SpaceText { get; }

    public double UsedPercent { get; }

    public bool IsRemovable { get; }

    public bool IsFolder { get; }

    public bool IsDrive => !IsFolder && !IsRemovable;

    public bool HasSpace => SpaceText is not null;

    /// <summary>Memoria collegata mentre l'app era aperta.</summary>
    [ObservableProperty]
    public partial bool IsNew { get; set; }
}

public partial class SourceViewModel : ViewModelBase
{
    private readonly IFolderPicker _folderPicker;
    private readonly Action<ScanResult, SourceItemViewModel> _onScanned;
    private readonly MediaScanner _scanner = new(new MetadataCaptureTimeReader());
    private readonly DispatcherTimer _watcher;
    private SourceItemViewModel? _chosenFolder;
    private CancellationTokenSource? _scan;
    private bool _detecting;

    public SourceViewModel(IFolderPicker folderPicker, Action<ScanResult, SourceItemViewModel> onScanned)
    {
        _folderPicker = folderPicker;
        _onScanned = onScanned;
        ApplySources(StorageSources.Detect(), markNew: false);

        // Le memorie collegate o scollegate compaiono e spariscono da sole.
        _watcher = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _watcher.Tick += async (_, _) => await RefreshSourcesAsync(markNew: true);
        _watcher.Start();
    }

    public ObservableCollection<SourceItemViewModel> Sources { get; } = [];

    public bool HasNoSources => Sources.Count == 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    public partial SourceItemViewModel? SelectedSource { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand), nameof(BrowseCommand))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? MessageTitle { get; set; }

    [ObservableProperty]
    public partial string? MessageDetail { get; set; }

    [ObservableProperty]
    public partial bool MessageIsError { get; set; }

    public bool HasMessage => MessageTitle is not null;

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseAsync()
    {
        if (await _folderPicker.PickFolderAsync(Strings.FolderPickerTitle) is not { } folder)
            return;

        if (_chosenFolder is not null)
            Sources.Remove(_chosenFolder);
        _chosenFolder = new SourceItemViewModel(folder);
        Sources.Add(_chosenFolder);
        SelectedSource = _chosenFolder;
        OnPropertyChanged(nameof(HasNoSources));
    }

    private bool CanBrowse() => !IsScanning;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (SelectedSource is not { } source)
            return;

        ClearMessage();
        IsScanning = true;
        _scan = new CancellationTokenSource();
        var progress = new Progress<ScanProgress>(p =>
            ProgressText = Format.Text(Strings.ScanProgress, Format.Number(p.FilesSeen), Format.Number(p.MediaFound)));

        try
        {
            var token = _scan.Token;
            AppLog.Info($"Scansione di {source.RootPath}");
            var result = await Task.Run(() => _scanner.Scan(source.RootPath, progress, token), token);
            AppLog.Info($"Scansione finita: {result.Files.Count} foto e video, {result.OtherFiles} altri file, {result.Issues.Count} avvisi");
            foreach (var issue in result.Issues)
                AppLog.Warning($"Scansione {issue.Kind}: {issue.Path} {issue.Detail}");

            if (result.HasMedia)
                _onScanned(result, source);
            else if (result.OtherFiles == 0)
                ShowMessage(Strings.ScanNothingFoundTitle, Format.Text(Strings.ScanEmptySourceDetail, source.Label), isError: false);
            else
                ShowMessage(Strings.ScanNothingFoundTitle,
                    Format.Text(Strings.ScanNothingFoundDetail, source.Label, Format.Number(result.OtherFiles)), isError: false);
        }
        catch (SourceNotAvailableException)
        {
            AppLog.Warning($"Sorgente {source.RootPath} non più disponibile durante la scansione");
            ShowMessage(Strings.ScanSourceGoneTitle, Format.Text(Strings.ScanSourceGoneDetail, source.Label), isError: true);
            if (source.IsFolder)
            {
                Sources.Remove(source);
                _chosenFolder = null;
                SelectedSource = null;
            }
        }
        catch (OperationCanceledException)
        {
            ShowMessage(Strings.ScanCanceled, null, isError: false);
        }
        catch (Exception e)
        {
            // Ultima rete di sicurezza: un errore imprevisto non deve chiudere l'app.
            AppLog.Error($"Scansione di {source.RootPath} interrotta da un errore imprevisto", e);
            ShowMessage(Strings.ScanFailedTitle, Format.Text(Strings.ScanFailedDetail, e.Message), isError: true);
        }
        finally
        {
            _scan.Dispose();
            _scan = null;
            IsScanning = false;
            ProgressText = null;
        }
    }

    private bool CanScan() => SelectedSource is not null && !IsScanning;

    [RelayCommand]
    private void CancelScan() => _scan?.Cancel();

    private async Task RefreshSourcesAsync(bool markNew)
    {
        if (IsScanning || _detecting)
            return;

        _detecting = true;
        try
        {
            // Interrogare le unità può richiedere qualche secondo (lettori vuoti, dischi in standby):
            // lo facciamo fuori dal thread dell'interfaccia.
            ApplySources(await Task.Run(StorageSources.Detect), markNew);
        }
        finally
        {
            _detecting = false;
        }
    }

    private void ApplySources(System.Collections.Generic.IReadOnlyList<StorageSource> detected, bool markNew)
    {
        var known = Sources.Where(s => !s.IsFolder).Select(s => s.RootPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var current = detected.Select(d => d.RootPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var gone in Sources.Where(s => !s.IsFolder && !current.Contains(s.RootPath)).ToList())
        {
            Sources.Remove(gone);
            if (SelectedSource == gone)
                SelectedSource = null;
        }

        foreach (var source in detected.Where(d => !known.Contains(d.RootPath)))
        {
            var item = new SourceItemViewModel(source) { IsNew = markNew };
            Sources.Insert(Sources.Count(s => !s.IsFolder), item);

            // Una scheda appena collegata è quasi sempre quella da importare.
            if (markNew || (SelectedSource is null && source.IsRemovable))
                SelectedSource = item;
        }

        OnPropertyChanged(nameof(HasNoSources));
    }

    /// <summary>Per una nuova importazione: via i messaggi della volta precedente e le scritte "appena collegata".</summary>
    public void Reset()
    {
        ClearMessage();
        foreach (var source in Sources)
            source.IsNew = false;
    }

    /// <summary>Riseleziona la sorgente appena usata e segnala quanti file contiene ancora.</summary>
    public void ShowRemaining(string root, string label, int files)
    {
        Reset();
        SelectedSource = Sources.FirstOrDefault(s => string.Equals(s.RootPath, root, StringComparison.OrdinalIgnoreCase)) ?? SelectedSource;
        ShowMessage(Format.Text(Strings.SourceRemainingTitle, label, Format.Number(files)), Strings.SourceRemainingDetail, isError: false);
    }

    private void ShowMessage(string title, string? detail, bool isError)
    {
        MessageTitle = title;
        MessageDetail = detail;
        MessageIsError = isError;
    }

    private void ClearMessage()
    {
        MessageTitle = null;
        MessageDetail = null;
    }
}
