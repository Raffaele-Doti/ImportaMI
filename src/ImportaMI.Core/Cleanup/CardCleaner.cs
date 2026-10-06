using ImportaMI.Core.Import;

namespace ImportaMI.Core.Cleanup;

public enum CleanupOutcome
{
    /// <summary>Eliminato dalla scheda dopo aver ricontrollato la copia.</summary>
    Deleted,

    /// <summary>Protetto in fotocamera (sola lettura): l'utente l'ha voluto conservare, quindi resta.</summary>
    KeptProtected,

    /// <summary>L'originale è cambiato dopo la copia, o non c'è più: resta com'è.</summary>
    KeptSourceChanged,

    /// <summary>La copia sul computer non c'è più: l'originale resta.</summary>
    KeptCopyMissing,

    /// <summary>La copia sul computer non coincide più con l'originale: l'originale resta.</summary>
    KeptCopyDifferent,

    /// <summary>Eliminazione non riuscita (errore del disco).</summary>
    Failed,

    /// <summary>Non raggiunto, perché la pulizia si è fermata prima.</summary>
    NotProcessed,
}

public sealed record CleanupResult(FileResult Item, CleanupOutcome Outcome, string? Error = null);

public enum CleanupStop
{
    None,
    Canceled,
    SourceDisconnected,
}

public sealed class CleanupReport
{
    public required IReadOnlyList<CleanupResult> Results { get; init; }

    public required CleanupStop Stop { get; init; }

    public int Deleted => Results.Count(r => r.Outcome == CleanupOutcome.Deleted);

    public long FreedBytes => Results.Where(r => r.Outcome == CleanupOutcome.Deleted).Sum(r => r.Item.SourceSize);

    public IEnumerable<CleanupResult> Kept => Results.Where(r => r.Outcome is not (CleanupOutcome.Deleted or CleanupOutcome.NotProcessed));
}

public readonly record struct CleanupProgress(int Done, int Total, long FreedBytes, string CurrentFile);

/// <summary>Elimina dalla scheda solo i file che sono al sicuro sul computer.</summary>
public static class CardCleaner
{
    /// <summary>File eliminabili: copiati e verificati, o già presenti identici, delle sessioni indicate.</summary>
    public static IReadOnlyList<FileResult> Candidates(ImportReport report, IReadOnlySet<int> sessionIndexes) =>
        report.Results.Where(r => r.IsSafe && r.FinalPath is not null && sessionIndexes.Contains(r.File.SessionIndex)).ToList();

    /// <summary>
    /// Per ogni file, subito prima di eliminarlo: controlla che l'originale non sia cambiato,
    /// che non sia protetto, che la copia esista e che il suo hash coincida ancora.
    /// </summary>
    public static async Task<CleanupReport> RunAsync(
        string sourceRoot,
        IReadOnlyList<FileResult> items,
        IProgress<CleanupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CleanupResult>(items.Count);
        var stop = CleanupStop.None;
        long freed = 0;

        foreach (var item in items)
        {
            if (stop != CleanupStop.None)
            {
                results.Add(new CleanupResult(item, CleanupOutcome.NotProcessed));
                continue;
            }

            progress?.Report(new CleanupProgress(results.Count, items.Count, freed, item.File.Source.FileName));
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var outcome = await CleanOneAsync(item, sourceRoot, cancellationToken);
                if (outcome == CleanupOutcome.Deleted)
                    freed += item.SourceSize;
                results.Add(new CleanupResult(item, outcome));
            }
            catch (OperationCanceledException)
            {
                stop = CleanupStop.Canceled;
                results.Add(new CleanupResult(item, CleanupOutcome.NotProcessed));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (!Directory.Exists(sourceRoot))
                {
                    stop = CleanupStop.SourceDisconnected;
                    results.Add(new CleanupResult(item, CleanupOutcome.NotProcessed));
                    continue;
                }
                results.Add(new CleanupResult(item, CleanupOutcome.Failed, e.Message));
            }
        }

        progress?.Report(new CleanupProgress(results.Count, items.Count, freed, string.Empty));
        return new CleanupReport { Results = results, Stop = stop };
    }

    private static async Task<CleanupOutcome> CleanOneAsync(FileResult item, string sourceRoot, CancellationToken cancellationToken)
    {
        var source = new FileInfo(item.File.Source.SourcePath);
        if (!source.Exists)
        {
            // Se manca la radice la scheda è stata scollegata: lo segnala il chiamante.
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException(sourceRoot);
            return CleanupOutcome.KeptSourceChanged;
        }

        if (source.Length != item.SourceSize || source.LastWriteTimeUtc != item.SourceWriteTimeUtc)
            return CleanupOutcome.KeptSourceChanged;

        if (source.Attributes.HasFlag(FileAttributes.ReadOnly))
            return CleanupOutcome.KeptProtected;

        var copy = new FileInfo(item.FinalPath!);
        if (!copy.Exists)
            return CleanupOutcome.KeptCopyMissing;
        if (copy.Length != item.SourceSize || await FileHasher.HashAsync(copy.FullName, cancellationToken) != item.Hash)
            return CleanupOutcome.KeptCopyDifferent;

        source.Delete();
        return CleanupOutcome.Deleted;
    }
}
