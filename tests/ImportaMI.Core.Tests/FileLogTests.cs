using ImportaMI.Core.Logging;

namespace ImportaMI.Core.Tests;

public class FileLogTests
{
    [Fact]
    public void Writes_one_file_per_day_with_level_and_exception()
    {
        using var folder = new TempSource();
        var log = new FileLog(folder.Root, () => new DateTime(2026, 10, 6, 9, 30, 0));

        log.Info("Scansione di E:\\");
        log.Error("Copia interrotta", new IOException("disco pieno"));

        var path = Path.Combine(folder.Root, "importami-20261006.log");
        var text = File.ReadAllText(path);
        Assert.Contains("2026-10-06 09:30:00.000 INFO  Scansione di E:\\", text);
        Assert.Contains("ERROR Copia interrotta", text);
        Assert.Contains("disco pieno", text);
    }

    [Fact]
    public void Old_logs_are_deleted_and_recent_ones_kept()
    {
        using var folder = new TempSource();
        var old = folder.WriteText("importami-20260801.log", "vecchio");
        var recent = folder.WriteText("importami-20261001.log", "recente");
        var other = folder.WriteText("note.txt", "non è un log");

        new FileLog(folder.Root, () => new DateTime(2026, 10, 6)).DeleteOlderThan(30);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void A_log_that_cannot_be_written_never_throws()
    {
        using var folder = new TempSource();
        // Un file al posto della cartella dei log rende impossibile scrivere.
        var blocked = folder.WriteText("logs", "sono un file");

        var log = new FileLog(blocked);
        log.Info("niente eccezioni");
        log.DeleteOlderThan(30);
    }
}
