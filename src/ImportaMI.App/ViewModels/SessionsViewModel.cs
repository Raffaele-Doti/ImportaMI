using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportaMI.App.Localization;
using ImportaMI.App.Resources;
using ImportaMI.Core.AI;
using ImportaMI.Core.Archive;
using ImportaMI.Core.Import;
using ImportaMI.Core.Media;
using ImportaMI.Core.Scanning;
using ImportaMI.Core.Sessions;
using System.Collections.Concurrent;
using System.Threading;

namespace ImportaMI.App.ViewModels;

/// <summary>Una sessione nella lista, con nome modificabile e selezione.</summary>
public partial class SessionItemViewModel : ObservableObject
{
    private readonly SessionsViewModel _owner;

    public SessionItemViewModel(ShootingSession session, string name, bool isSelected, bool canMerge, SessionsViewModel owner)
    {
        _owner = owner;
        Session = session;
        Name = name;
        IsSelected = isSelected;
        CanMerge = canMerge;

        var photos = session.Shots.Count(s => !s.IsVideo);
        var videos = session.Shots.Count - photos;
        var fileDated = session.Shots.Count(s => s.TimeSource == CaptureTimeSource.FileDate);

        DateText = Format.Date(session.Start);
        TimeRange = $"{Format.Time(session.Start)} → {Format.Time(session.End)}";
        DurationText = Format.Duration(session.Duration);
        PhotosText = Format.Text(Strings.SessionPhotos, Format.Number(photos));
        VideosText = videos > 0 ? Format.Text(Strings.SessionVideos, Format.Number(videos)) : null;
        FilesText = Format.Text(Strings.SessionFiles, Format.Number(session.FileCount));
        SizeText = Format.Size(session.SizeBytes);
        FileDatesText = fileDated > 0 ? Format.Text(Strings.SessionFileDates, Format.Number(fileDated)) : null;
        CanSplit = session.Shots.Count > 1;

        // Fino a 8 scatti distribuiti lungo la sessione; le miniature arrivano in background.
        var count = Math.Min(8, session.Shots.Count);
        for (var i = 0; i < count; i++)
        {
            var shot = session.Shots[count == 1 ? 0 : i * (session.Shots.Count - 1) / (count - 1)];
            Thumbnails.Add(new ThumbnailViewModel(shot));
        }
    }

    public List<ThumbnailViewModel> Thumbnails { get; } = [];

    public ShootingSession Session { get; }

    public string DateText { get; }

    public string TimeRange { get; }

    public string DurationText { get; }

    public string PhotosText { get; }

    public string? VideosText { get; }

    public string FilesText { get; }

    public string SizeText { get; }

    public string? FileDatesText { get; }

    public bool CanSplit { get; }

    public bool CanMerge { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Percorso della cartella che verrà creata, relativo alla destinazione.</summary>
    [ObservableProperty]
    public partial string FolderPreview { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    public partial string? Warning { get; set; }

    public bool HasWarning => Warning is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArchiveText))]
    public partial string? ArchiveText { get; set; }

    [ObservableProperty]
    public partial string? ArchiveFolders { get; set; }

    [ObservableProperty]
    public partial bool IsFullyArchived { get; set; }

    public bool HasArchiveText => ArchiveText is not null;

    public ObservableCollection<AiTagViewModel> AiTags { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiTags))]
    public partial bool HasAiTags { get; set; }

    [ObservableProperty]
    public partial string? SuggestedName { get; set; }

    public void UpdateAiTags(IReadOnlyList<SessionTag> tags, string suggestedName, bool isUserEdited)
    {
        AiTags.Clear();
        foreach (var tag in tags)
        {
            AiTags.Add(new AiTagViewModel(tag, SetNameFromTag));
        }
        HasAiTags = AiTags.Count > 0;
        SuggestedName = suggestedName;

        if (!isUserEdited && string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(suggestedName))
        {
            Name = suggestedName;
        }
    }

    private void SetNameFromTag(string tagCategory)
    {
        Name = tagCategory;
    }

    partial void OnNameChanged(string value) => _owner.OnRenamed(this);

    partial void OnIsSelectedChanged(bool value) => _owner.OnSelectionChanged(this);

    [RelayCommand]
    private void Split() => _owner.OpenSplitter(this);

    [RelayCommand]
    private void Merge() => _owner.MergeWithPrevious(this);
}

/// <summary>Miniatura di uno scatto: segnaposto finché l'immagine non è pronta, o se non c'è.</summary>
public partial class ThumbnailViewModel : ObservableObject
{
    public ThumbnailViewModel(Shot shot)
    {
        Shot = shot;
        IsVideo = shot.IsVideo;
        Badge = shot.IsVideo ? string.Empty : shot.HasRaw && shot.HasJpeg ? "RAW+JPG" : shot.HasRaw ? "RAW" : string.Empty;
        TimeText = Format.Time(shot.CaptureTime);
        _ = LoadAsync(shot);
    }

    public Shot Shot { get; }

    public bool IsVideo { get; }

    public string Badge { get; }

    public string TimeText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    public partial Avalonia.Media.Imaging.Bitmap? Image { get; set; }

    [ObservableProperty]
    public partial Avalonia.Media.RotateTransform? Rotation { get; set; }

    /// <summary>Il tentativo di caricare la miniatura è finito (riuscito o no).</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    public bool HasImage => Image is not null;

    private async Task LoadAsync(Shot shot)
    {
        if (!shot.IsVideo && await Services.Thumbnails.ForShotAsync(shot) is { } thumbnail)
        {
            Rotation = thumbnail.Rotation == 0 ? null : new Avalonia.Media.RotateTransform(thumbnail.Rotation);
            Image = thumbnail.Image;
        }
        IsLoaded = true;
    }
}

/// <summary>Uno scatto nell'editor di divisione: cliccandolo, da lì inizia una nuova sessione.</summary>
public partial class SplitTileViewModel(ThumbnailViewModel thumbnail, bool isFirst, Action changed) : ObservableObject
{
    public ThumbnailViewModel Thumbnail { get; } = thumbnail;

    /// <summary>Il primo scatto apre già la sessione: non si può scegliere.</summary>
    public bool IsFirst { get; } = isFirst;

    [ObservableProperty]
    public partial bool IsStart { get; set; }

    [RelayCommand]
    private void Toggle()
    {
        if (IsFirst)
            return;
        IsStart = !IsStart;
        changed();
    }
}

/// <summary>Tutte le foto di una sessione, per scegliere dove dividerla.</summary>
public partial class SplitEditorViewModel : ObservableObject
{
    private readonly Action<IReadOnlyList<string>> _apply;
    private readonly Action _cancel;

    public SplitEditorViewModel(SessionItemViewModel session, Action<IReadOnlyList<string>> apply, Action cancel)
    {
        _apply = apply;
        _cancel = cancel;
        Title = Format.Text(Strings.SplitTitle, session.Name);
        Tiles = session.Session.Shots.Select((shot, i) => new SplitTileViewModel(new ThumbnailViewModel(shot), i == 0, Refresh)).ToList();
        Refresh();
    }

    public string Title { get; }

    public IReadOnlyList<SplitTileViewModel> Tiles { get; }

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial bool HasSplits { get; set; }

    [RelayCommand(CanExecute = nameof(HasSplits))]
    private void Apply() => _apply(Tiles.Where(t => t.IsStart).Select(t => SessionBuilder.KeyOf(t.Thumbnail.Shot)).ToList());

    [RelayCommand]
    private void Cancel() => _cancel();

    private void Refresh()
    {
        var splits = Tiles.Count(t => t.IsStart);
        HasSplits = splits > 0;
        CountText = Format.Text(Strings.SplitCount, Format.Number(splits + 1));
    }
}

/// <summary>Un avviso della scansione, da mostrare all'utente.</summary>
public sealed record IssueItem(string FileName, string Folder, string Message);

/// <summary>Passaggio 2: le sessioni proposte, da rinominare, dividere, unire e selezionare.</summary>
public partial class SessionsViewModel : ViewModelBase
{
    private const FolderLayout PreviewLayout = FolderLayout.YearThenDateAndName;

    private readonly IReadOnlyList<Shot> _shots;
    private readonly Action _back;
    private readonly Action _next;
    private readonly string _archiveRoot;

    // Scatti da cui inizia una sessione, nomi scelti e sessioni escluse, per chiave:
    // sopravvivono quando l'utente divide o unisce.
    private readonly HashSet<string> _starts;
    private readonly Dictionary<string, string> _names = [];
    private readonly HashSet<string> _excluded = [];
    private readonly HashSet<string> _touched = [];
    private readonly ConcurrentDictionary<string, IReadOnlyList<CategoryScore>> _shotClassifications = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _aiCts = new();
    private bool _rebuilding;

    public SessionsViewModel(ScanResult scan, string sourceLabel, string archiveRoot, Action back, Action next)
    {
        _shots = ShotBuilder.Build(scan.Files);
        _back = back;
        _next = next;
        _archiveRoot = archiveRoot;
        _starts = SessionBuilder.DayStarts(_shots);

        Subtitle = Format.Text(Strings.SessionsSubtitle, Format.Number(scan.Files.Count), sourceLabel);

        // Un RAW senza orario leggibile non è un problema se il JPEG dello stesso scatto ce l'ha:
        // lo scatto è datato correttamente, quindi l'avviso sarebbe solo rumore.
        var datedByTwin = _shots
            .Where(s => s.TimeSource == CaptureTimeSource.Metadata)
            .SelectMany(s => s.Files)
            .Select(f => f.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Issues = scan.Issues
            .Where(i => i.Kind is not (ScanIssueKind.NoCaptureTime or ScanIssueKind.MetadataUnreadable) || !datedByTwin.Contains(i.Path))
            .Select(i => new IssueItem(Path.GetFileName(i.Path), Path.GetDirectoryName(i.Path) ?? string.Empty, DisplayNames.For(i.Kind)))
            .ToList();
        IssuesHeader = Format.Text(Strings.IssuesHeader, Format.Number(Issues.Count));

        Rebuild();
        _ = CheckArchiveAsync();
        _ = StartAiAnalysisAsync(_aiCts.Token);
    }

    public string Subtitle { get; }

    public IReadOnlyList<IssueItem> Issues { get; }

    public string IssuesHeader { get; }

    public bool HasIssues => Issues.Count > 0;

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    /// <summary>Copie già presenti nell'archivio, per percorso sorgente; vuoto finché il controllo non è finito.</summary>
    public IReadOnlyDictionary<string, string> ArchiveCopies { get; private set; } = new Dictionary<string, string>();

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial bool HasSelection { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial bool IsCheckingArchive { get; set; }

    [ObservableProperty]
    public partial string? ArchiveStatus { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial bool IsAiAnalyzing { get; set; }

    [ObservableProperty]
    public partial double AiProgressPercent { get; set; }

    [ObservableProperty]
    public partial string AiProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasAiStatus { get; set; }

    /// <summary>Editor aperto per dividere una sessione, oppure <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSplitting), nameof(IsListing))]
    public partial SplitEditorViewModel? Splitter { get; set; }

    public bool IsSplitting => Splitter is not null;

    public bool IsListing => Splitter is null;

    public IEnumerable<SessionItemViewModel> Selected => Sessions.Where(s => s.IsSelected);

    /// <summary>Le sessioni selezionate con il nome scelto, per il piano di copia.</summary>
    public IReadOnlyList<SessionChoice> Choices() =>
        Selected.Select(s => new SessionChoice(s.Session, s.Name)).ToList();

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    [RelayCommand]
    private void Back()
    {
        _aiCts.Cancel();
        _back();
    }

    // Si aspetta il controllo dell'archivio e l'analisi AI di tutte le foto prima di proseguire.
    [RelayCommand(CanExecute = nameof(CanContinue))]
    private void Next() => _next();

    private bool CanContinue() => HasSelection && !IsCheckingArchive && !IsAiAnalyzing;

    internal void OnRenamed(SessionItemViewModel item)
    {
        if (_rebuilding)
            return;
        _names[item.Session.Key] = item.Name;
        RefreshFolders();
    }

    internal void OnSelectionChanged(SessionItemViewModel item)
    {
        if (_rebuilding)
            return;
        _touched.Add(item.Session.Key);
        if (item.IsSelected)
            _excluded.Remove(item.Session.Key);
        else
            _excluded.Add(item.Session.Key);
        RefreshFolders();
    }

    internal void OpenSplitter(SessionItemViewModel item) =>
        Splitter = new SplitEditorViewModel(
            item,
            apply: keys =>
            {
                _starts.UnionWith(keys);
                Splitter = null;
                Rebuild();
            },
            cancel: () => Splitter = null);

    /// <summary>Unisce la sessione alla precedente: il nome resta quello della precedente.</summary>
    internal void MergeWithPrevious(SessionItemViewModel item)
    {
        if (!item.CanMerge)
            return;
        _starts.Remove(item.Session.Key);
        Rebuild();
    }

    private void Rebuild()
    {
        _rebuilding = true;
        Sessions.Clear();
        var sessions = SessionBuilder.Build(_shots, _starts);
        for (var i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            // Senza nome finché l'utente non ne sceglie uno: la cartella usa solo la data.
            var name = _names.TryGetValue(session.Key, out var chosen) ? chosen : string.Empty;
            var item = new SessionItemViewModel(session, name, !_excluded.Contains(session.Key), canMerge: i > 0, this);
            DescribeArchive(item);
            ApplyAiAnalysisToItem(item);
            Sessions.Add(item);
        }
        _rebuilding = false;

        CountText = Format.Text(Strings.SessionsCount, Format.Number(Sessions.Count));
        RefreshFolders();
    }

    private void ApplyAiAnalysisToItem(SessionItemViewModel item)
    {
        var shotScores = item.Session.Shots
            .Where(s => !s.IsVideo && _shotClassifications.TryGetValue(SessionBuilder.KeyOf(s), out _))
            .Select(s => _shotClassifications[SessionBuilder.KeyOf(s)])
            .ToList();

        if (shotScores.Count > 0)
        {
            var analysis = SessionTagger.Aggregate(shotScores);
            item.UpdateAiTags(analysis.Tags, analysis.SuggestedName, _touched.Contains(item.Session.Key));
        }
    }

    private sealed record AiProgress(double Percent, string StatusText, string? UpdatedSessionKey, IReadOnlyList<SessionTag>? Tags, string? SuggestedName);

    private async Task StartAiAnalysisAsync(CancellationToken ct)
    {
        var photos = _shots.Where(s => !s.IsVideo).ToList();
        var total = photos.Count;
        if (total == 0)
            return;

        IsAiAnalyzing = true;
        HasAiStatus = true;
        AiProgressPercent = 0;
        AiProgressText = Format.Text(Strings.AiAnalyzing, Format.Number(0), Format.Number(total), 0);

        var progress = new Progress<AiProgress>(p =>
        {
            AiProgressPercent = p.Percent;
            AiProgressText = p.StatusText;
            if (p.UpdatedSessionKey is not null && p.Tags is not null)
            {
                var item = Sessions.FirstOrDefault(s => s.Session.Key == p.UpdatedSessionKey);
                if (item is not null)
                {
                    item.UpdateAiTags(p.Tags, p.SuggestedName ?? string.Empty, _touched.Contains(p.UpdatedSessionKey));
                    RefreshFolders();
                }
            }
        });

        try
        {
            await Task.Run(() =>
            {
                var analyzed = 0;
                var currentSessions = SessionBuilder.Build(_shots, _starts);
                var sessionByShotKey = new Dictionary<string, ShootingSession>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in currentSessions)
                {
                    foreach (var shot in s.Shots)
                    {
                        sessionByShotKey[SessionBuilder.KeyOf(shot)] = s;
                    }
                }

                var classifier = AiClassifier.Default;

                foreach (var shot in photos)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    var shotKey = SessionBuilder.KeyOf(shot);
                    var scores = classifier.ClassifyShot(shot);
                    _shotClassifications[shotKey] = scores;

                    analyzed++;
                    var percent = (double)analyzed / total * 100.0;
                    var text = Format.Text(Strings.AiAnalyzing, Format.Number(analyzed), Format.Number(total), (int)percent);

                    string? sessionKeyToUpdate = null;
                    IReadOnlyList<SessionTag>? tags = null;
                    string? suggestedName = null;

                    if (sessionByShotKey.TryGetValue(shotKey, out var session))
                    {
                        sessionKeyToUpdate = session.Key;
                        var sessionShotScores = session.Shots
                            .Where(sh => !sh.IsVideo && _shotClassifications.TryGetValue(SessionBuilder.KeyOf(sh), out _))
                            .Select(sh => _shotClassifications[SessionBuilder.KeyOf(sh)])
                            .ToList();

                        var analysis = SessionTagger.Aggregate(sessionShotScores);
                        tags = analysis.Tags;
                        suggestedName = analysis.SuggestedName;
                    }

                    ((IProgress<AiProgress>)progress).Report(new AiProgress(percent, text, sessionKeyToUpdate, tags, suggestedName));
                }
            }, ct);

            AiProgressText = Format.Text(Strings.AiComplete, Format.Number(total));
            AiProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            Services.AppLog.Error("Errore durante l'analisi AI", e);
        }
        finally
        {
            IsAiAnalyzing = false;
        }
    }

    private async Task CheckArchiveAsync()
    {
        IsCheckingArchive = true;
        ArchiveStatus = Format.Text(Strings.ArchiveChecking, _archiveRoot);
        var files = _shots.SelectMany(s => s.Files).ToList();
        var progress = new Progress<ArchiveProgress>(p => ArchiveStatus = p.Total == 0
            ? Format.Text(Strings.ArchiveIndexing, Format.Number(p.FilesIndexed))
            : Format.Text(Strings.ArchiveComparing, Format.Number(p.FilesChecked), Format.Number(p.Total)));

        try
        {
            var root = _archiveRoot;
            ArchiveCopies = await Task.Run(() => ArchiveIndex.Build(root, progress).FindCopies(files, progress));
        }
        catch (Exception e)
        {
            // Senza controllo si può comunque importare: la copia salta i file identici nella destinazione.
            Services.AppLog.Error($"Controllo dell'archivio {_archiveRoot} non riuscito", e);
            ArchiveStatus = Format.Text(Strings.ArchiveFailed, e.Message);
            IsCheckingArchive = false;
            return;
        }

        var archivedShots = _shots.Count(s => s.Files.All(f => ArchiveCopies.ContainsKey(f.SourcePath)));
        Services.AppLog.Info($"Controllo dell'archivio {_archiveRoot}: {archivedShots} scatti su {_shots.Count} già presenti");
        ArchiveStatus = archivedShots == 0
            ? Format.Text(Strings.ArchiveNone, _archiveRoot)
            : Format.Text(Strings.ArchiveFound, Format.Number(archivedShots), _archiveRoot);

        // Le sessioni già importate per intero si deselezionano, se l'utente non le ha già toccate.
        foreach (var item in Sessions)
        {
            DescribeArchive(item);
            if (item.IsFullyArchived && !_touched.Contains(item.Session.Key))
                _excluded.Add(item.Session.Key);
        }
        Rebuild();
        IsCheckingArchive = false;
    }

    private void DescribeArchive(SessionItemViewModel item)
    {
        if (ArchiveCopies.Count == 0)
            return;

        var shots = item.Session.Shots;
        var found = shots.Where(s => s.Files.All(f => ArchiveCopies.ContainsKey(f.SourcePath))).ToList();
        if (found.Count == 0)
            return;

        item.IsFullyArchived = found.Count == shots.Count;
        item.ArchiveText = item.IsFullyArchived
            ? Strings.ArchiveAll
            : Format.Text(Strings.ArchiveSome, Format.Number(found.Count), Format.Number(shots.Count));
        var folders = found
            .SelectMany(s => s.Files)
            .Select(f => Path.GetDirectoryName(ArchiveCopies[f.SourcePath]) ?? _archiveRoot)
            .Select(folder => Path.GetRelativePath(_archiveRoot, folder) is var relative && relative != "." ? relative : folder)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        item.ArchiveFolders = Format.Text(Strings.ArchiveWhere, string.Join(" · ", folders));
    }

    private void SetAll(bool selected)
    {
        _rebuilding = true;
        foreach (var item in Sessions)
        {
            item.IsSelected = selected;
            _touched.Add(item.Session.Key);
            if (selected)
                _excluded.Remove(item.Session.Key);
            else
                _excluded.Add(item.Session.Key);
        }
        _rebuilding = false;
        RefreshFolders();
    }

    /// <summary>Aggiorna percorsi, avvisi sui nomi e riepilogo dopo ogni modifica.</summary>
    private void RefreshFolders()
    {
        var folders = Sessions.ToDictionary(
            s => s,
            s => string.Join(Path.DirectorySeparatorChar, FolderNaming.RelativeSegments(s.Session.Start, s.Name, PreviewLayout)));
        var taken = Selected
            .GroupBy(s => folders[s].ToUpperInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        foreach (var item in Sessions)
        {
            item.FolderPreview = folders[item];
            var trimmed = item.Name.Trim();
            item.Warning =
                trimmed.Length > 0 && FolderNaming.Sanitize(trimmed) != trimmed ? Strings.WarningInvalidChars
                : item.IsSelected && taken.Contains(folders[item].ToUpperInvariant()) ? Strings.WarningDuplicate
                : null;
        }

        var selected = Selected.ToList();
        HasSelection = selected.Count > 0;
        Summary = Format.Text(
            Strings.SessionsSummary,
            Format.Number(selected.Count),
            Format.Number(Sessions.Count),
            Format.Number(selected.Sum(s => s.Session.FileCount)),
            Format.Size(selected.Sum(s => s.Session.SizeBytes)));
    }
}
