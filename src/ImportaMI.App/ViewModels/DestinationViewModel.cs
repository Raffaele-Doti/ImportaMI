using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportaMI.App.Localization;
using ImportaMI.App.Resources;
using ImportaMI.App.Services;
using ImportaMI.Core.Import;
using ImportaMI.Core.Media;
using ImportaMI.Core.Scanning;
using ImportaMI.Core.Settings;

namespace ImportaMI.App.ViewModels;

/// <summary>Passaggio 3: dove e come salvare le sessioni.</summary>
public partial class DestinationViewModel : ViewModelBase
{
    private readonly IFolderPicker _folderPicker;
    private readonly string _sourceRoot;
    private readonly Action<ImportPlan, ImportOptions, ImportSettings> _start;
    private readonly Action _back;
    private IReadOnlyList<SessionChoice> _sessions = [];
    private IReadOnlyDictionary<string, string> _archiveCopies = new Dictionary<string, string>();
    private bool _ready;

    public DestinationViewModel(
        IFolderPicker folderPicker,
        string sourceRoot,
        ImportSettings settings,
        Action<ImportPlan, ImportOptions, ImportSettings> start,
        Action back)
    {
        _folderPicker = folderPicker;
        _sourceRoot = sourceRoot;
        _start = start;
        _back = back;

        DestinationPath = string.IsNullOrWhiteSpace(settings.DestinationRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : settings.DestinationRoot;
        IncludeVideos = settings.IncludeVideos;
        Verify = settings.Verify;
        ProposeCleanup = settings.ProposeCleanup;
        EjectWhenDone = settings.EjectWhenDone;
        SkipArchiveDuplicates = settings.SkipArchiveDuplicates;

        Formats = new(settings.Photos, Refresh,
            (PhotoFormats.RawAndJpeg, Strings.FormatRawAndJpeg, Strings.FormatRawAndJpegDetail),
            (PhotoFormats.RawOnly, Strings.FormatRawOnly, Strings.FormatRawOnlyDetail),
            (PhotoFormats.JpegOnly, Strings.FormatJpegOnly, Strings.FormatJpegOnlyDetail));
        Layouts = new(settings.Layout, Refresh,
            (FolderLayout.DateAndName, Strings.LayoutDateAndName, "2026-09-27 …"),
            (FolderLayout.YearThenDateAndName, Strings.LayoutYear, Path.Combine("2026", "2026-09-27 …")),
            (FolderLayout.YearMonthThenDateAndName, Strings.LayoutYearMonth, Path.Combine("2026", "09", "2026-09-27 …")));
        Subfolders = new(settings.Subfolders, Refresh,
            (SubfolderMode.None, Strings.SubfolderModeNone, null),
            (SubfolderMode.PhotosAndVideos, Strings.SubfolderModePhotosAndVideos, $"{Strings.SubfolderPhotos} · {Strings.SubfolderVideos}"),
            (SubfolderMode.ByFormat, Strings.SubfolderModeByFormat, $"RAW · JPG · {Strings.SubfolderVideos}"));
        Duplicates = new(settings.Duplicates, Refresh,
            (DuplicatePolicy.SkipIdentical, Strings.DupSkip, Strings.DupSkipDetail),
            (DuplicatePolicy.RenameAlways, Strings.DupRename, Strings.DupRenameDetail),
            (DuplicatePolicy.Overwrite, Strings.DupOverwrite, Strings.DupOverwriteDetail));
        _ready = true;
    }

    public ChoiceGroup<PhotoFormats> Formats { get; }

    public ChoiceGroup<FolderLayout> Layouts { get; }

    public ChoiceGroup<SubfolderMode> Subfolders { get; }

    public ChoiceGroup<DuplicatePolicy> Duplicates { get; }

    [ObservableProperty]
    public partial bool SkipArchiveDuplicates { get; set; }

    [ObservableProperty]
    public partial string DestinationPath { get; set; }

    [ObservableProperty]
    public partial bool IncludeVideos { get; set; }

    [ObservableProperty]
    public partial bool Verify { get; set; }

    [ObservableProperty]
    public partial bool ProposeCleanup { get; set; }

    [ObservableProperty]
    public partial bool EjectWhenDone { get; set; }

    [ObservableProperty]
    public partial string TreeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FoldersText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpaceTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SpaceText { get; set; }

    [ObservableProperty]
    public partial double UsedPercent { get; set; }

    [ObservableProperty]
    public partial double NeededPercent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public bool HasError => Error is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string? Note { get; set; }

    public bool HasNote => Note is not null;

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    public partial bool CanStart { get; set; }

    public ImportPlan? Plan { get; private set; }

    /// <summary>Le sessioni scelte nel passaggio 2: si aggiornano ogni volta che l'utente torna avanti.</summary>
    public void SetSessions(IReadOnlyList<SessionChoice> sessions, IReadOnlyDictionary<string, string> archiveCopies)
    {
        _sessions = sessions;
        _archiveCopies = archiveCopies;
        Refresh();
    }

    partial void OnDestinationPathChanged(string value) => Refresh();

    partial void OnIncludeVideosChanged(bool value) => Refresh();

    partial void OnSkipArchiveDuplicatesChanged(bool value) => Refresh();

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _folderPicker.PickFolderAsync(Strings.DestPickerTitle) is { } folder)
            DestinationPath = folder;
    }

    [RelayCommand]
    private void Back() => _back();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (Plan is null)
            return;
        _start(Plan, Options(), CurrentSettings());
    }

    public ImportOptions Options() => new()
    {
        DestinationRoot = FullPathOrSelf(DestinationPath),
        Layout = Layouts.Value,
        Format = new ImportFormat(Formats.Value, IncludeVideos),
        Subfolders = Subfolders.Value,
        SubfolderNames = DisplayNames.Subfolders,
        Duplicates = Duplicates.Value,
        Verify = Verify,
        SkipArchiveDuplicates = SkipArchiveDuplicates,
    };

    public ImportSettings CurrentSettings() => new()
    {
        DestinationRoot = DestinationPath,
        Layout = Layouts.Value,
        Photos = Formats.Value,
        IncludeVideos = IncludeVideos,
        Subfolders = Subfolders.Value,
        Duplicates = Duplicates.Value,
        Verify = Verify,
        EjectWhenDone = EjectWhenDone,
        ProposeCleanup = ProposeCleanup,
        SkipArchiveDuplicates = SkipArchiveDuplicates,
    };

    private void Refresh()
    {
        if (!_ready)
            return;

        Error = Validate(DestinationPath, out var destination);
        if (destination is null)
        {
            Plan = null;
            TreeText = string.Empty;
            FoldersText = string.Empty;
            SpaceText = null;
            Note = null;
            Summary = string.Empty;
            CanStart = false;
            return;
        }

        var plan = ImportPlanner.Create(_sourceRoot, _sessions, Options(), _archiveCopies);
        Plan = plan;
        TreeText = BuildTree(plan);
        FoldersText = Format.Text(Strings.DestFolders, Format.Number(plan.Sessions.Count));
        Summary = Format.Text(Strings.DestSummary, Format.Number(plan.Sessions.Count), Format.Number(plan.Files.Count), Format.Size(plan.TotalBytes));

        // Spazio: quello che serve deve stare in quello che è libero.
        var space = StorageSources.SpaceFor(destination);
        if (space is { } s)
        {
            SpaceTitle = Format.Text(Strings.DestSpace, s.Root);
            SpaceText = Format.Text(Strings.DestSpaceDetail, Format.Size(plan.BytesToCopy), Format.Size(s.Free), Format.Size(s.Total));
            UsedPercent = 100.0 * (s.Total - s.Free) / Math.Max(1, s.Total);
            // Cumulativo: la barra ambra (occupato + importazione) sta sotto quella grigia (occupato).
            NeededPercent = Math.Min(100, UsedPercent + (100.0 * plan.BytesToCopy / Math.Max(1, s.Total)));
            if (Error is null && plan.BytesToCopy > s.Free)
                Error = Format.Text(Strings.DestNoSpace, Format.Size(plan.BytesToCopy), Format.Size(s.Free));
        }
        else
        {
            SpaceText = null;
            Error ??= Format.Text(Strings.DestDriveMissing, Path.GetPathRoot(destination) ?? destination);
        }

        Note = BuildNote(plan, destination);
        CanStart = Error is null && plan.Files.Count > 0;
    }

    /// <summary>Restituisce il messaggio d'errore, oppure <c>null</c> se il percorso va bene.</summary>
    private string? Validate(string path, out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(path))
            return Strings.DestEmpty;
        if (!Path.IsPathFullyQualified(path))
            return Strings.DestNotAbsolute;

        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Strings.DestInvalid;
        }

        if (fullPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            fullPath = null;
            return Strings.DestInvalid;
        }

        // Copiare sulla scheda stessa riempirebbe la sorgente e renderebbe pericolosa la pulizia.
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_sourceRoot)) + Path.DirectorySeparatorChar;
        var target = Path.TrimEndingDirectorySeparator(fullPath) + Path.DirectorySeparatorChar;
        if (target.StartsWith(source, comparison))
            return Strings.DestOnSource;

        return null;
    }

    private string? BuildNote(ImportPlan plan, string destination)
    {
        var notes = new List<string>();
        if (!Directory.Exists(destination))
            notes.Add(Strings.DestWillCreate);

        var shotsWithoutFiles = plan.Sessions.Sum(s => s.ShotsWithoutFiles);
        if (Formats.Value == PhotoFormats.JpegOnly && shotsWithoutFiles > 0)
            notes.Add(Format.Text(Strings.DestJpegOnlyNote, Format.Number(shotsWithoutFiles)));

        if (plan.AlreadyInArchive > 0)
            notes.Add(Format.Text(Strings.DestArchiveNote, Format.Number(plan.AlreadyInArchive)));

        var onCard = _sessions.Sum(s => s.Session.FileCount);
        var left = onCard - plan.Files.Count;
        if (left > 0)
            notes.Add(Format.Text(Strings.DestLeftOnCard, Format.Number(left)));

        return notes.Count == 0 ? null : string.Join(Environment.NewLine, notes);
    }

    private static string BuildTree(ImportPlan plan)
    {
        // Albero testuale delle cartelle, come in un terminale.
        var root = new Node();
        foreach (var session in plan.Sessions)
        {
            var node = root;
            foreach (var segment in session.RelativeSegments)
                node = node.Child(segment);
            node.Sessions.Add(session);
        }

        var text = new StringBuilder(plan.DestinationRoot);
        Write(root, string.Empty, plan, text);
        return text.ToString();
    }

    private static void Write(Node node, string indent, ImportPlan plan, StringBuilder text)
    {
        var children = node.Children.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase).ToList();
        for (var i = 0; i < children.Count; i++)
        {
            var (name, child) = (children[i].Key, children[i].Value);
            var last = i == children.Count - 1;
            text.AppendLine().Append(indent).Append(last ? "└─ " : "├─ ").Append(name);
            if (child.Sessions.Count > 0)
                text.Append("   ").Append(Format.Text(Strings.SessionFiles, Format.Number(child.Sessions.Sum(s => s.FileCount))));

            var childIndent = indent + (last ? "   " : "│  ");
            if (child.Sessions.Count > 0)
            {
                // Sottocartelle reali, ricavate dai percorsi del piano.
                var folders = child.Sessions.Select(s => s.FolderPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var subfolders = plan.Files
                    .Where(f => folders.Contains(Path.GetDirectoryName(Path.GetDirectoryName(f.TargetPath)) ?? string.Empty))
                    .GroupBy(f => Path.GetFileName(Path.GetDirectoryName(f.TargetPath)) ?? string.Empty)
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                for (var j = 0; j < subfolders.Count; j++)
                {
                    text.AppendLine().Append(childIndent).Append(j == subfolders.Count - 1 ? "└─ " : "├─ ")
                        .Append(subfolders[j].Key).Append("   ").Append(Format.Number(subfolders[j].Count()));
                }
            }

            Write(child, childIndent, plan, text);
        }
    }

    private static string FullPathOrSelf(string path)
    {
        try { return Path.GetFullPath(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return path; }
    }

    private sealed class Node
    {
        public Dictionary<string, Node> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<PlannedSession> Sessions { get; } = [];

        public Node Child(string name) => Children.TryGetValue(name, out var node) ? node : Children[name] = new Node();
    }
}
