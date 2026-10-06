using System.IO.Hashing;

namespace ImportaMI.Core.Import;

/// <summary>Hash xxHash64 dei file: veloce quanto la lettura dal disco, adatto a verificare copie.</summary>
public static class FileHasher
{
    public const int BufferSize = 1 << 20;

    public static async Task<ulong> HashAsync(string path, CancellationToken cancellationToken = default)
    {
        var hash = new XxHash64();
        var buffer = new byte[BufferSize];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            hash.Append(buffer.AsSpan(0, read));
        return hash.GetCurrentHashAsUInt64();
    }
}

/// <summary>Permette di mettere in pausa un'operazione lunga tra un blocco e l'altro.</summary>
public sealed class PauseGate
{
    private readonly object _lock = new();
    private TaskCompletionSource? _paused;

    public bool IsPaused
    {
        get { lock (_lock) return _paused is not null; }
    }

    public void Pause()
    {
        lock (_lock)
            _paused ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Resume()
    {
        lock (_lock)
        {
            _paused?.TrySetResult();
            _paused = null;
        }
    }

    public Task WaitAsync(CancellationToken cancellationToken)
    {
        Task? wait;
        lock (_lock)
            wait = _paused?.Task;
        return wait is null ? Task.CompletedTask : wait.WaitAsync(cancellationToken);
    }
}
