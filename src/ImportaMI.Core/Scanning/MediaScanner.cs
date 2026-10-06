using ImportaMI.Core.Media;

namespace ImportaMI.Core.Scanning;

/// <summary>Tipo di problema incontrato durante la scansione. Nessuno ferma la scansione.</summary>
public enum ScanIssueKind
{
    /// <summary>Cartella che non si è potuta aprire (permessi, errore di lettura): il contenuto è stato saltato.</summary>
    FolderNotAccessible,

    /// <summary>File che non si è potuto leggere: è stato saltato.</summary>
    FileNotReadable,

    /// <summary>File vuoto (0 byte), tipico di una scrittura interrotta: è stato saltato.</summary>
    EmptyFile,

    /// <summary>Metadati presenti ma senza orario di scatto: è stata usata la data del file.</summary>
    NoCaptureTime,

    /// <summary>Metadati illeggibili o formato non riconosciuto: è stata usata la data del file.</summary>
    MetadataUnreadable,
}

public sealed record ScanIssue(string Path, ScanIssueKind Kind, string? Detail = null);

public readonly record struct ScanProgress(int FilesSeen, int MediaFound, string CurrentFolder);

public sealed class ScanResult
{
    public required string Root { get; init; }

    public required IReadOnlyList<MediaFile> Files { get; init; }

    public required IReadOnlyList<ScanIssue> Issues { get; init; }

    /// <summary>File che non sono foto né video (documenti, musica, file della fotocamera…).</summary>
    public required int OtherFiles { get; init; }

    public bool HasMedia => Files.Count > 0;
}

/// <summary>La sorgente non esiste o non è più raggiungibile (per esempio la scheda è stata scollegata).</summary>
public sealed class SourceNotAvailableException(string path, Exception? inner = null)
    : IOException($"La sorgente '{path}' non è disponibile.", inner)
{
    public string SourcePath { get; } = path;
}

/// <summary>Cerca foto e video in una cartella e nelle sue sottocartelle.</summary>
public sealed class MediaScanner(ICaptureTimeReader reader)
{
    private const int ProgressEvery = 25;

    // Cartelle di sistema di Windows, macOS e Linux che non contengono mai foto da importare.
    private static readonly HashSet<string> SystemFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "$RECYCLE.BIN", "System Volume Information", "RECYCLER",
        ".Trashes", ".Trash", ".Spotlight-V100", ".fseventsd", ".TemporaryItems", ".DocumentRevisions-V100",
        "lost+found",
    };

    private static readonly EnumerationOptions ListOneFolder = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

    /// <summary>
    /// Scansiona <paramref name="root"/>. Gli errori su singole cartelle o file finiscono in
    /// <see cref="ScanResult.Issues"/> senza interrompere la scansione.
    /// </summary>
    /// <exception cref="SourceNotAvailableException">La sorgente non esiste o è stata scollegata.</exception>
    /// <exception cref="OperationCanceledException">La scansione è stata annullata.</exception>
    public ScanResult Scan(string root, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!System.IO.Directory.Exists(root))
            throw new SourceNotAvailableException(root);

        var files = new List<MediaFile>();
        var issues = new List<ScanIssue>();
        var otherFiles = 0;
        var seen = 0;

        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(root));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();

            FileSystemInfo[] entries;
            try
            {
                entries = folder.GetFileSystemInfos("*", ListOneFolder);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Se sparisce la radice, la sorgente è stata scollegata: inutile proseguire.
                if (!System.IO.Directory.Exists(root))
                    throw new SourceNotAvailableException(root, e);

                issues.Add(new ScanIssue(folder.FullName, ScanIssueKind.FolderNotAccessible, e.Message));
                continue;
            }

            progress?.Report(new ScanProgress(seen, files.Count, folder.FullName));

            // Ordine inverso così le sottocartelle si visitano in ordine alfabetico.
            foreach (var subfolder in entries.OfType<DirectoryInfo>().OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (ShouldVisit(subfolder))
                    pending.Push(subfolder);
            }

            foreach (var file in entries.OfType<FileInfo>().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                seen++;

                var kind = IsMacMetadataFile(file) ? null : MediaKinds.FromPath(file.Name);
                if (kind is null)
                {
                    otherFiles++;
                    continue;
                }

                if (TryRead(file, kind.Value, root, issues) is { } media)
                    files.Add(media);

                if (seen % ProgressEvery == 0)
                    progress?.Report(new ScanProgress(seen, files.Count, folder.FullName));
            }
        }

        progress?.Report(new ScanProgress(seen, files.Count, root));
        return new ScanResult { Root = root, Files = files, Issues = issues, OtherFiles = otherFiles };
    }

    private MediaFile? TryRead(FileInfo file, MediaKind kind, string root, List<ScanIssue> issues)
    {
        try
        {
            if (file.Length == 0)
            {
                issues.Add(new ScanIssue(file.FullName, ScanIssueKind.EmptyFile));
                return null;
            }

            var result = reader.Read(file.FullName, kind);
            if (result.Time is { } time)
                return new MediaFile(file.FullName, kind, time, file.Length);

            issues.Add(new ScanIssue(
                file.FullName,
                result.Error is null ? ScanIssueKind.NoCaptureTime : ScanIssueKind.MetadataUnreadable,
                result.Error));
            return new MediaFile(file.FullName, kind, file.LastWriteTime, file.Length, CaptureTimeSource.FileDate);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Se è sparita la radice la scheda è stata scollegata: ogni file seguente fallirebbe
            // allo stesso modo, quindi ci fermiamo invece di elencarli tutti come illeggibili.
            if (!System.IO.Directory.Exists(root))
                throw new SourceNotAvailableException(root, e);

            issues.Add(new ScanIssue(file.FullName, ScanIssueKind.FileNotReadable, e.Message));
            return null;
        }
    }

    private static bool ShouldVisit(DirectoryInfo folder) =>
        !SystemFolders.Contains(folder.Name)
        // Collegamenti simbolici e junction possono creare cicli o portare fuori dalla sorgente.
        && !folder.Attributes.HasFlag(FileAttributes.ReparsePoint);

    // macOS scrive file "._IMG_0001.JPG" accanto agli originali sui dischi FAT/exFAT:
    // hanno l'estensione di una foto ma contengono solo metadati del Finder.
    private static bool IsMacMetadataFile(FileInfo file) => file.Name.StartsWith("._", StringComparison.Ordinal);
}
