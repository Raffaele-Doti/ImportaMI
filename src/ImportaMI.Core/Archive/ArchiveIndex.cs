using ImportaMI.Core.Import;
using ImportaMI.Core.Media;

namespace ImportaMI.Core.Archive;

public readonly record struct ArchiveProgress(int FilesIndexed, int FilesChecked, int Total);

/// <summary>
/// Le foto e i video già presenti nell'archivio dell'utente (la cartella di destinazione).
/// Serve a non importare due volte la stessa foto, anche se nell'archivio ha un altro nome o sta in un'altra cartella.
/// </summary>
/// <remarks>
/// Il confronto è sul contenuto: i candidati sono i file con la stessa dimensione esatta in byte,
/// e una corrispondenza vale solo se l'impronta xxHash64 dell'intero file coincide.
/// Nome e data non contano: due foto diverse con lo stesso nome non sono mai considerate uguali,
/// e la stessa foto rinominata viene riconosciuta.
/// </remarks>
public sealed class ArchiveIndex
{
    private readonly Dictionary<long, List<string>> _bySize;
    private readonly Dictionary<string, ulong> _hashes = new(StringComparer.OrdinalIgnoreCase);

    private ArchiveIndex(string root, Dictionary<long, List<string>> bySize, int count)
    {
        Root = root;
        _bySize = bySize;
        FileCount = count;
    }

    public string Root { get; }

    public int FileCount { get; }

    public static ArchiveIndex Empty(string root) => new(root, [], 0);

    /// <summary>
    /// Elenca le foto e i video dell'archivio con la loro dimensione. Cartelle illeggibili e collegamenti
    /// vengono saltati; un archivio che non esiste ancora è semplicemente vuoto.
    /// </summary>
    public static ArchiveIndex Build(string root, IProgress<ArchiveProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root))
            return Empty(root);

        var bySize = new Dictionary<long, List<string>>();
        var count = 0;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", options))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MediaKinds.FromPath(file.Name) is null
                || file.Name.StartsWith("._", StringComparison.Ordinal)
                || file.Name.EndsWith(ImportRunner.PartialSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                if (!bySize.TryGetValue(file.Length, out var list))
                    bySize[file.Length] = list = [];
                list.Add(file.FullName);
                if (++count % 500 == 0)
                    progress?.Report(new ArchiveProgress(count, 0, 0));
            }
            catch (IOException)
            {
                // File sparito durante l'elenco: non ci interessa.
            }
        }

        return new ArchiveIndex(root, bySize, count);
    }

    /// <summary>
    /// Per ogni file della sorgente cerca una copia identica nell'archivio. Restituisce sorgente → copia trovata.
    /// Si legge per intero solo chi ha la stessa dimensione esatta, quindi di solito pochissimi file.
    /// </summary>
    public IReadOnlyDictionary<string, string> FindCopies(
        IReadOnlyList<MediaFile> files,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];
            if (_bySize.TryGetValue(file.SizeBytes, out var candidates)
                && FindCopy(file, candidates, cancellationToken) is { } copy)
                found[file.SourcePath] = copy;

            if (i % 10 == 0)
                progress?.Report(new ArchiveProgress(FileCount, i, files.Count));
        }

        progress?.Report(new ArchiveProgress(FileCount, files.Count, files.Count));
        return found;
    }

    private string? FindCopy(MediaFile file, List<string> candidates, CancellationToken cancellationToken)
    {
        ulong? fingerprint = null;

        // Prima i file con lo stesso nome: se c'è una copia, di solito è lì.
        foreach (var path in candidates.OrderBy(p => string.Equals(Path.GetFileName(p), file.FileName, StringComparison.OrdinalIgnoreCase) ? 0 : 1))
        {
            // Se la sorgente sta dentro l'archivio, il file non deve "trovare se stesso".
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(file.SourcePath), StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                fingerprint ??= Fingerprint(file.SourcePath, cancellationToken);
                if (!_hashes.TryGetValue(path, out var archived))
                    _hashes[path] = archived = Fingerprint(path, cancellationToken);
                if (archived == fingerprint)
                    return path;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Un file illeggibile non è una copia su cui contare.
            }
        }

        return null;
    }

    private static ulong Fingerprint(string path, CancellationToken cancellationToken) =>
        FileHasher.HashAsync(path, cancellationToken).GetAwaiter().GetResult();
}
