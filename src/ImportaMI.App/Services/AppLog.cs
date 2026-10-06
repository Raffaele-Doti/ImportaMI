using System;
using ImportaMI.Core.Logging;

namespace ImportaMI.App.Services;

/// <summary>
/// Log dell'app. Finché <see cref="Start"/> non viene chiamato (per esempio nei test) non scrive nulla.
/// </summary>
public static class AppLog
{
    /// <summary>Giorni di log conservati.</summary>
    private const int KeepDays = 30;

    private static FileLog? _log;

    public static string? Folder => _log?.Folder;

    public static void Start(FileLog log)
    {
        _log = log;
        log.DeleteOlderThan(KeepDays);
    }

    public static void Info(string message) => _log?.Info(message);

    public static void Warning(string message) => _log?.Warning(message);

    public static void Error(string message, Exception? exception = null) => _log?.Error(message, exception);
}
