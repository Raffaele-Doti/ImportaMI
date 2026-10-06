using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportaMI.App.Resources;
using ImportaMI.App.Services;
using ImportaMI.Core;
using ImportaMI.Core.Import;
using ImportaMI.Core.Scanning;
using ImportaMI.Core.Settings;

namespace ImportaMI.App.ViewModels;

/// <summary>Un passaggio nella barra laterale.</summary>
public partial class StepItem(int number, string title) : ObservableObject
{
    public int Number { get; } = number;

    public string Title { get; } = title;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial bool IsDone { get; set; }

    [ObservableProperty]
    public partial bool IsAvailable { get; set; }
}

/// <summary>Coordina i cinque passaggi e ricorda le preferenze.</summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IFolderPicker _folderPicker;
    private readonly SettingsStore _settingsStore;
    private readonly AppDataFolder _dataFolder;
    private readonly Func<string, Task<bool>> _eject;
    private ImportSettings _settings;
    private readonly SourceViewModel _source;
    private SourceInfo? _sourceInfo;
    private ScanResult? _scan;
    private SessionsViewModel? _sessions;
    private DestinationViewModel? _destination;
    private CopyViewModel? _copy;
    private CleanupViewModel? _cleanup;
    private int _step;

    /// <param name="dataFolder">Dove salvare impostazioni e report; i test passano una cartella temporanea.</param>
    public MainViewModel(
        IFolderPicker folderPicker,
        SettingsStore? settingsStore = null,
        Func<string, Task<bool>>? eject = null,
        AppDataFolder? dataFolder = null)
    {
        _folderPicker = folderPicker;
        _dataFolder = dataFolder ?? AppDataFolder.Default;
        if (settingsStore is null)
        {
            settingsStore = new SettingsStore(_dataFolder.SettingsFile);
            settingsStore.MigrateFrom(SettingsStore.LegacyPath);
        }
        _settingsStore = settingsStore;
        var ejectVolume = eject ?? (root => VolumeEjector.TryEjectAsync(root));
        _eject = async root =>
        {
            var ejected = await ejectVolume(root);
            if (ejected)
                AppLog.Info($"Espulsa {root}");
            else
                AppLog.Warning($"Espulsione di {root} non riuscita");
            return ejected;
        };
        _settings = _settingsStore.Load();
        Steps =
        [
            new StepItem(1, Strings.StepSource),
            new StepItem(2, Strings.StepSessions),
            new StepItem(3, Strings.StepDestination),
            new StepItem(4, Strings.StepCopy),
            new StepItem(5, Strings.StepCleanup),
        ];
        _source = new SourceViewModel(_folderPicker, OnScanCompleted);
        CurrentPage = _source;
        Show(1, _source);
    }

    public IReadOnlyList<StepItem> Steps { get; }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    /// <summary>Dopo l'avvio della copia i passaggi 1-3 sono chiusi: si riparte con "Nuova importazione".</summary>
    private bool CopyStarted => _copy is not null;

    [RelayCommand]
    private void GoToStep(StepItem step)
    {
        if (!step.IsAvailable || step.Number == _step)
            return;
        switch (step.Number)
        {
            case 1: Show(1, _source); break;
            case 2: Show(2, _sessions!); break;
            case 3: ShowDestination(); break;
            case 4: Show(4, _copy!); break;
            case 5: ShowCleanup(); break;
        }
    }

    private void OnScanCompleted(ScanResult result, SourceItemViewModel source)
    {
        _scan = result;
        _sourceInfo = new SourceInfo(source.RootPath, source.Label, CanEject: !source.IsFolder);
        _destination = null;
        _sessions = new SessionsViewModel(result, source.Label, ArchiveRoot, back: () => Show(1, _source), next: ShowDestination);
        _sessions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SessionsViewModel.HasSelection))
                RefreshSteps();
        };
        Show(2, _sessions);
    }

    private void ShowDestination()
    {
        _destination ??= new DestinationViewModel(_folderPicker, _sourceInfo!.Root, _settings, StartCopy, back: () => Show(2, _sessions!));
        _destination.SetSessions(_sessions!.Choices(), _sessions.ArchiveCopies);
        Show(3, _destination);
    }

    private void StartCopy(ImportPlan plan, ImportOptions options, ImportSettings settings)
    {
        _settings = settings;
        _settingsStore.Save(settings);
        AppLog.Info($"Copia avviata da {_sourceInfo!.Root} a {options.DestinationRoot}: {plan.Sessions.Count} sessioni, " +
                    $"{plan.Files.Count} file ({plan.AlreadyInArchive} già nell'archivio), {plan.BytesToCopy:N0} byte da copiare; " +
                    $"formati {options.Format.Photos}, video {options.Format.IncludeVideos}, sottocartelle {options.Subfolders}, " +
                    $"struttura {options.Layout}, duplicati {options.Duplicates}, verifica {options.Verify}");
        _copy = new CopyViewModel(plan, options, _sourceInfo!, settings.ProposeCleanup, settings.EjectWhenDone, ShowCleanup, NewImport, _eject, _dataFolder.Reports);
        // A copia finita si apre il passaggio 5 nella barra laterale.
        _copy.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CopyViewModel.IsFinished))
                RefreshSteps();
        };
        Show(4, _copy);
        _ = _copy.StartAsync();
    }

    private void ShowCleanup()
    {
        if (_copy?.Report is not { } report)
            return;
        _cleanup ??= new CleanupViewModel(report, _scan!, _sourceInfo!, _settings.EjectWhenDone, NewImport, _eject);
        Show(5, _cleanup);
    }

    /// <summary>L'archivio in cui cercare i duplicati: l'ultima destinazione usata, o la cartella Immagini.</summary>
    private string ArchiveRoot => string.IsNullOrWhiteSpace(_settings.DestinationRoot)
        ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        : _settings.DestinationRoot;

    /// <summary>
    /// Torna al passo 1. Se sulla stessa scheda restano foto o video, la lascia selezionata
    /// e lo segnala, così l'utente non se ne dimentica.
    /// </summary>
    private void NewImport()
    {
        if (_sourceInfo is { } source && _scan is { } scan)
        {
            var deleted = _cleanup?.DeletedCount ?? 0;
            var remaining = scan.Files.Count - deleted;
            if (remaining > 0 && System.IO.Directory.Exists(source.Root))
                _source.ShowRemaining(source.Root, source.Label, remaining);
            else
                _source.Reset();
        }
        else
        {
            _source.Reset();
        }

        _scan = null;
        _sourceInfo = null;
        _sessions = null;
        _destination = null;
        _copy = null;
        _cleanup = null;
        Show(1, _source);
    }

    private void Show(int step, ViewModelBase page)
    {
        _step = step;
        CurrentPage = page;
        RefreshSteps();
    }

    private void RefreshSteps()
    {
        foreach (var item in Steps)
        {
            item.IsActive = item.Number == _step;
            item.IsDone = item.Number < _step;
            item.IsAvailable = item.Number switch
            {
                1 => !CopyStarted,
                2 => !CopyStarted && _sessions is not null,
                3 => !CopyStarted && _sessions?.HasSelection == true,
                4 => CopyStarted,
                _ => _copy?.Report is not null && _copy.IsFinished,
            };
        }
    }
}
