using System.Globalization;
using System.Text;

namespace ImportaMI.Core.Logging;

/// <summary>
/// Log di testo, un file al giorno (<c>importami-aaaaMMgg.log</c>). Non solleva mai eccezioni:
/// un log che non si riesce a scrivere non deve interrompere un'importazione.
/// </summary>
public sealed class FileLog
{
    private readonly object _lock = new();
    private readonly Func<DateTime> _now;

    public FileLog(string folder, Func<DateTime>? now = null)
    {
        Folder = folder;
        _now = now ?? (() => DateTime.Now);
    }

    public string Folder { get; }

    public string CurrentFile => Path.Combine(Folder, $"importami-{_now():yyyyMMdd}.log");

    public void Info(string message) => Write("INFO ", message, null);

    public void Warning(string message) => Write("WARN ", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    /// <summary>Elimina i log più vecchi di <paramref name="days"/> giorni.</summary>
    public void DeleteOlderThan(int days)
    {
        try
        {
            if (!Directory.Exists(Folder))
                return;
            var limit = _now().Date.AddDays(-days);
            foreach (var file in Directory.EnumerateFiles(Folder, "importami-*.log"))
            {
                var stamp = Path.GetFileNameWithoutExtension(file)["importami-".Length..];
                if (DateTime.TryParseExact(stamp, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day < limit)
                    File.Delete(file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void Write(string level, string message, Exception? exception)
    {
        var line = new StringBuilder()
            .Append(_now().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append(' ').Append(level).Append(' ').Append(message);
        if (exception is not null)
            line.AppendLine().Append(exception);

        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(CurrentFile, line.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
