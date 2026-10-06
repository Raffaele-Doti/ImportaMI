using ImportaMI.Core.Cleanup;
using ImportaMI.Core.Import;

namespace ImportaMI.Core.Tests;

public class CardCleanerTests
{
    private static async Task<(ImportFixture Fx, ImportReport Report)> ImportedCard()
    {
        var fx = new ImportFixture();
        var options = fx.Options();
        var report = await new ImportRunner().RunAsync(fx.Plan(options), options);
        return (fx, report);
    }

    private static IReadOnlySet<int> AllSessions(ImportReport report) => report.Plan.Sessions.Select(s => s.Index).ToHashSet();

    [Fact]
    public async Task Deletes_only_files_that_are_safe_on_the_computer()
    {
        var (fx, report) = await ImportedCard();
        using var _ = fx;
        fx.Card.WriteText(Path.Combine("MISC", "CANONMSC.CTG"), "indice");

        var cleanup = await CardCleaner.RunAsync(fx.Card.Root, CardCleaner.Candidates(report, AllSessions(report)));

        Assert.Equal(5, cleanup.Deleted);
        Assert.Equal(report.Results.Sum(r => r.SourceSize), cleanup.FreedBytes);
        Assert.Empty(Directory.GetFiles(Path.Combine(fx.Card.Root, "DCIM"), "*", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(fx.Card.Root, "MISC", "CANONMSC.CTG")));
        Assert.All(report.Results, r => Assert.True(File.Exists(r.FinalPath)));
    }

    [Fact]
    public async Task Only_the_chosen_sessions_are_cleaned()
    {
        var (fx, report) = await ImportedCard();
        using var _ = fx;

        var cleanup = await CardCleaner.RunAsync(fx.Card.Root, CardCleaner.Candidates(report, new HashSet<int> { 1 }));

        Assert.Equal(1, cleanup.Deleted);
        Assert.Equal(4, Directory.GetFiles(fx.Card.Root, "*", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task File_protected_in_camera_is_kept()
    {
        var (fx, report) = await ImportedCard();
        using var _ = fx;
        var protectedPhoto = report.Results[0].File.Source.SourcePath;
        File.SetAttributes(protectedPhoto, FileAttributes.ReadOnly);

        var cleanup = await CardCleaner.RunAsync(fx.Card.Root, CardCleaner.Candidates(report, AllSessions(report)));

        Assert.Equal(CleanupOutcome.KeptProtected, cleanup.Results[0].Outcome);
        Assert.True(File.Exists(protectedPhoto));
    }

    [Fact]
    public async Task Original_is_kept_if_the_copy_was_changed_or_removed()
    {
        var (fx, report) = await ImportedCard();
        using var _ = fx;
        var changed = report.Results[0];
        var removed = report.Results[1];
        var bytes = File.ReadAllBytes(changed.FinalPath!);
        bytes[^1] ^= 0xFF; // stessa dimensione, contenuto diverso
        File.WriteAllBytes(changed.FinalPath!, bytes);
        File.Delete(removed.FinalPath!);

        var cleanup = await CardCleaner.RunAsync(fx.Card.Root, CardCleaner.Candidates(report, AllSessions(report)));

        Assert.Equal(CleanupOutcome.KeptCopyDifferent, cleanup.Results[0].Outcome);
        Assert.Equal(CleanupOutcome.KeptCopyMissing, cleanup.Results[1].Outcome);
        Assert.True(File.Exists(changed.File.Source.SourcePath));
        Assert.True(File.Exists(removed.File.Source.SourcePath));
    }

    [Fact]
    public async Task Original_changed_after_the_copy_is_kept()
    {
        var (fx, report) = await ImportedCard();
        using var _ = fx;
        File.AppendAllText(report.Results[0].File.Source.SourcePath, "modificato in camera");

        var cleanup = await CardCleaner.RunAsync(fx.Card.Root, CardCleaner.Candidates(report, AllSessions(report)));

        Assert.Equal(CleanupOutcome.KeptSourceChanged, cleanup.Results[0].Outcome);
    }

    [Fact]
    public async Task Failed_copies_are_never_candidates()
    {
        var (fx, report) = await ImportedCard();
        using var _ = fx;
        var withFailure = new ImportReport
        {
            Plan = report.Plan,
            Results = [report.Results[0] with { Outcome = FileOutcome.Failed }, .. report.Results.Skip(1)],
            StopReason = StopReason.None,
            Started = report.Started,
            Finished = report.Finished,
        };

        var candidates = CardCleaner.Candidates(withFailure, AllSessions(report));

        Assert.DoesNotContain(withFailure.Results[0], candidates);
    }

    [Fact]
    public async Task Card_disconnected_during_cleanup_stops_it()
    {
        var (fx, report) = await ImportedCard();
        using var _ = fx;
        fx.Card.Dispose();

        var cleanup = await CardCleaner.RunAsync(fx.Card.Root, CardCleaner.Candidates(report, AllSessions(report)));

        Assert.Equal(CleanupStop.SourceDisconnected, cleanup.Stop);
        Assert.Equal(0, cleanup.Deleted);
    }
}
