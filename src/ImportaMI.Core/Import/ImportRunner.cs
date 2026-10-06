using System.Diagnostics;
using System.IO.Hashing;

namespace ImportaMI.Core.Import;

/// <summary>Perché un file non è stato copiato. Il dettaglio del sistema operativo è in <see cref="FileResult.Error"/>.</summary>
public enum FileFailure
{
    None,

    /// <summary>Errore di lettura o scrittura.</summary>
    ReadOrWrite,

    /// <summary>La copia riletta non coincide con l'originale.</summary>
    VerificationMismatch,

    /// <summary>Il file non c'è più sulla sorgente.</summary>
    SourceMissing,
}

public enum FileOutcome
{
    /// <summary>Copiato (e verificato, se la verifica è attiva).</summary>
    Copied,

    /// <summary>Nella destinazione c'era già un file identico: non serviva copiarlo.</summary>
    SkippedIdentical,

    /// <summary>Non copiato per un errore su questo file; si può riprovare.</summary>
    Failed,

    /// <summary>Non raggiunto, perché la copia si è fermata prima.</summary>
    NotProcessed,
}

/// <summary>Esito di un file. Hash, dimensione e data dell'originale servono alla pulizia della scheda.</summary>
public sealed record FileResult(
    PlannedFile File,
    FileOutcome Outcome,
    string? FinalPath = null,
    ulong Hash = 0,
    long SourceSize = 0,
    DateTime SourceWriteTimeUtc = default,
    string? Error = null,
    FileFailure Failure = FileFailure.None)
{
    /// <summary>Il file è al sicuro nella destinazione: copiato e verificato, oppure già presente identico.</summary>
    public bool IsSafe => Outcome is FileOutcome.Copied or FileOutcome.SkippedIdentical;
}

/// <summary>Perché la copia si è fermata prima della fine.</summary>
public enum StopReason
{
    None,
    Canceled,
    SourceDisconnected,
    DestinationFull,
    DestinationUnavailable,
}

public sealed class ImportReport
{
    public required ImportPlan Plan { get; init; }

    /// <summary>Un esito per ogni file del piano, nello stesso ordine.</summary>
    public required IReadOnlyList<FileResult> Results { get; init; }

    public required StopReason StopReason { get; init; }

    public required DateTime Started { get; init; }

    public required DateTime Finished { get; init; }

    public int Count(FileOutcome outcome) => Results.Count(r => r.Outcome == outcome);

    public long Bytes(FileOutcome outcome) => Results.Where(r => r.Outcome == outcome).Sum(r => r.File.Source.SizeBytes);

    public IEnumerable<FileResult> ForSession(int index) => Results.Where(r => r.File.SessionIndex == index);

    /// <summary>File da riprovare: falliti o mai raggiunti.</summary>
    public IReadOnlyList<FileResult> Retryable => Results.Where(r => r.Outcome is FileOutcome.Failed or FileOutcome.NotProcessed).ToList();
}

public readonly record struct CopyProgress(
    long BytesDone,
    long TotalBytes,
    int FilesDone,
    int TotalFiles,
    int SessionIndex,
    string CurrentFile,
    bool Verifying);

/// <summary>Copia i file del piano con le garanzie descritte nel README.</summary>
public sealed class ImportRunner
{
    /// <summary>Suffisso del file in scrittura: viene rinominato solo a copia completa e verificata.</summary>
    public const string PartialSuffix = ".importami-partial";

    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(80);

    public Task<ImportReport> RunAsync(
        ImportPlan plan,
        ImportOptions options,
        IProgress<CopyProgress>? progress = null,
        PauseGate? pause = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(plan, plan.Files, options, progress, pause, cancellationToken, previous: null);

    /// <summary>Riprova solo i file falliti o non raggiunti; gli altri esiti restano quelli di <paramref name="previous"/>.</summary>
    public Task<ImportReport> RetryAsync(
        ImportReport previous,
        ImportOptions options,
        IProgress<CopyProgress>? progress = null,
        PauseGate? pause = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(previous.Plan, previous.Retryable.Select(r => r.File).ToList(), options, progress, pause, cancellationToken, previous);

    private static async Task<ImportReport> RunAsync(
        ImportPlan plan,
        IReadOnlyList<PlannedFile> toCopy,
        ImportOptions options,
        IProgress<CopyProgress>? progress,
        PauseGate? pause,
        CancellationToken cancellationToken,
        ImportReport? previous)
    {
        var started = DateTime.Now;
        var results = new Dictionary<PlannedFile, FileResult>();
        var totalBytes = toCopy.Sum(f => f.Source.SizeBytes);
        long bytesDone = 0;
        var filesDone = 0;
        var stop = StopReason.None;
        var clock = Stopwatch.StartNew();
        var lastReport = TimeSpan.MinValue;

        void Report(PlannedFile file, bool verifying, bool force = false)
        {
            if (progress is null || (!force && clock.Elapsed - lastReport < ProgressInterval))
                return;
            lastReport = clock.Elapsed;
            progress.Report(new CopyProgress(bytesDone, totalBytes, filesDone, toCopy.Count, file.SessionIndex, file.Source.FileName, verifying));
        }

        foreach (var file in toCopy)
        {
            var bytesBefore = bytesDone;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pause is not null)
                    await pause.WaitAsync(cancellationToken);

                Report(file, verifying: false, force: true);
                results[file] = await CopyOneAsync(
                    file,
                    options,
                    added => { bytesDone += added; Report(file, verifying: false); },
                    () => Report(file, verifying: true, force: true),
                    pause,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                stop = StopReason.Canceled;
                break;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (!Directory.Exists(plan.SourceRoot))
                {
                    stop = StopReason.SourceDisconnected;
                    break;
                }

                if (IsDiskFull(e))
                {
                    stop = StopReason.DestinationFull;
                    break;
                }

                if (Path.GetPathRoot(plan.DestinationRoot) is { } destinationDrive && !Directory.Exists(destinationDrive))
                {
                    stop = StopReason.DestinationUnavailable;
                    break;
                }

                results[file] = new FileResult(file, FileOutcome.Failed, Error: e.Message,
                    Failure: e is FileNotFoundException ? FileFailure.SourceMissing : FileFailure.ReadOrWrite);
                // I byte del file fallito contano come "fatti", così la barra arriva comunque in fondo.
                bytesDone = bytesBefore + file.Source.SizeBytes;
            }

            filesDone++;
        }

        // Ricompone gli esiti nell'ordine del piano, tenendo quelli della corsa precedente.
        var attempted = toCopy.ToHashSet();
        var merged = plan.Files.Select((file, i) =>
            results.TryGetValue(file, out var result) ? result
            : previous?.Results[i] is { } old && !attempted.Contains(old.File) ? old
            : new FileResult(file, FileOutcome.NotProcessed)).ToList();

        if (toCopy.Count > 0)
            Report(toCopy[^1], verifying: false, force: true);

        return new ImportReport
        {
            Plan = plan,
            Results = merged,
            StopReason = stop,
            Started = previous?.Started ?? started,
            Finished = DateTime.Now,
        };
    }

    private static async Task<FileResult> CopyOneAsync(
        PlannedFile file,
        ImportOptions options,
        Action<long> onBytes,
        Action onVerifying,
        PauseGate? pause,
        CancellationToken cancellationToken)
    {
        var source = new FileInfo(file.Source.SourcePath);
        if (!source.Exists)
            throw new FileNotFoundException("Il file non è più sulla sorgente.", source.FullName);

        var size = source.Length;
        var writeTime = source.LastWriteTimeUtc;

        // Già nell'archivio, in un'altra cartella: se è identico per intero non lo si copia di nuovo.
        if (file.ExistingCopy is { } existing && File.Exists(existing) && new FileInfo(existing).Length == size)
        {
            var sourceHash = await FileHasher.HashAsync(source.FullName, cancellationToken);
            if (sourceHash == await FileHasher.HashAsync(existing, cancellationToken))
            {
                onBytes(size);
                return new FileResult(file, FileOutcome.SkippedIdentical, existing, sourceHash, size, writeTime);
            }
        }

        var target = file.TargetPath;
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        if (File.Exists(target))
        {
            if (options.Duplicates == DuplicatePolicy.SkipIdentical)
            {
                if (new FileInfo(target).Length == size)
                {
                    var sourceHash = await FileHasher.HashAsync(source.FullName, cancellationToken);
                    if (sourceHash == await FileHasher.HashAsync(target, cancellationToken))
                    {
                        onBytes(size);
                        return new FileResult(file, FileOutcome.SkippedIdentical, target, sourceHash, size, writeTime);
                    }
                }

                target = ImportPlanner.FreeName(target, File.Exists);
            }
            else if (options.Duplicates == DuplicatePolicy.RenameAlways)
            {
                target = ImportPlanner.FreeName(target, File.Exists);
            }
        }

        var partial = target + PartialSuffix;
        try
        {
            var hash = await CopyWithHashAsync(source.FullName, partial, onBytes, pause, cancellationToken);

            if (options.Verify)
            {
                onVerifying();
                if (await FileHasher.HashAsync(partial, cancellationToken) != hash)
                {
                    TryDelete(partial);
                    return new FileResult(file, FileOutcome.Failed, Failure: FileFailure.VerificationMismatch);
                }
            }

            File.Move(partial, target, overwrite: options.Duplicates == DuplicatePolicy.Overwrite);
            File.SetLastWriteTimeUtc(target, writeTime);
            return new FileResult(file, FileOutcome.Copied, target, hash, size, writeTime);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    private static async Task<ulong> CopyWithHashAsync(
        string source,
        string destination,
        Action<long> onBytes,
        PauseGate? pause,
        CancellationToken cancellationToken)
    {
        var hash = new XxHash64();
        var buffer = new byte[FileHasher.BufferSize];

        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, FileHasher.BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous))
        await using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, FileHasher.BufferSize, FileOptions.Asynchronous))
        {
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                hash.Append(buffer.AsSpan(0, read));
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                onBytes(read);
                if (pause is not null)
                    await pause.WaitAsync(cancellationToken);
            }

            // Svuota le cache del sistema sul disco prima di dichiarare la copia completa.
            output.Flush(flushToDisk: true);
        }

        return hash.GetCurrentHashAsUInt64();
    }

    private static bool IsDiskFull(Exception e) =>
        (e.HResult & 0xFFFF) is 0x70 or 0x27   // Windows: ERROR_DISK_FULL, ERROR_HANDLE_DISK_FULL
        || e.HResult == 28;                    // Linux e macOS: ENOSPC

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
