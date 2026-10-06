using ImportaMI.Core.Import;
using ImportaMI.Core.Media;
using ImportaMI.Core.Scanning;
using ImportaMI.Core.Sessions;

namespace ImportaMI.Core.Tests;

/// <summary>Una scheda finta, una destinazione vuota e il piano per copiare tutte le sessioni.</summary>
internal sealed class ImportFixture : IDisposable
{
    public ImportFixture(Action<TempSource>? fill = null)
    {
        fill ??= DefaultCard;
        fill(Card);
        var scan = new MediaScanner(new MetadataCaptureTimeReader()).Scan(Card.Root);
        // Una sessione per giorno, più una divisione davanti a ogni video (come farebbe l'utente).
        var shots = ShotBuilder.Build(scan.Files);
        var starts = SessionBuilder.DayStarts(shots);
        starts.UnionWith(shots.Where(s => s.IsVideo).Select(SessionBuilder.KeyOf));
        Sessions = SessionBuilder.Build(shots, starts);
    }

    public TempSource Card { get; } = new();

    public TempSource Destination { get; } = new();

    public IReadOnlyList<ShootingSession> Sessions { get; }

    public ImportOptions Options(Func<ImportOptions, ImportOptions>? change = null)
    {
        var options = new ImportOptions { DestinationRoot = Destination.Root, Layout = FolderLayout.DateAndName, SubfolderNames = new("Foto", "RAW", "JPG", "Video") };
        return change is null ? options : change(options);
    }

    public ImportPlan Plan(ImportOptions options) =>
        ImportPlanner.Create(Card.Root, Sessions.Select((s, i) => new SessionChoice(s, $"Sessione {i + 1}")).ToList(), options);

    public static readonly byte[] RawBytes = [.. Enumerable.Range(0, 5000).Select(i => (byte)(i % 251))];

    /// <summary>Due scatti RAW + JPEG al mattino e un video la sera.</summary>
    public static void DefaultCard(TempSource card)
    {
        var dcim = Path.Combine("DCIM", "100CANON");
        card.Write(Path.Combine(dcim, "IMG_0001.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 07:42:00"));
        card.Write(Path.Combine(dcim, "IMG_0001.CR3"), RawBytes);
        card.Write(Path.Combine(dcim, "IMG_0002.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 07:50:00"));
        card.Write(Path.Combine(dcim, "IMG_0002.CR3"), RawBytes);
        var video = card.Write(Path.Combine(dcim, "MVI_0003.MP4"), [.. Enumerable.Repeat((byte)7, 3000)]);
        File.SetLastWriteTime(video, new DateTime(2026, 9, 27, 21, 0, 0));
    }

    public void Dispose()
    {
        Card.Dispose();
        Destination.Dispose();
    }
}

public class ImportRunnerTests
{
    private static readonly ImportRunner Runner = new();

    [Fact]
    public async Task Copies_every_file_into_its_session_folder_and_subfolder()
    {
        using var fx = new ImportFixture();
        var options = fx.Options();
        var report = await Runner.RunAsync(fx.Plan(options), options);

        Assert.Equal(StopReason.None, report.StopReason);
        Assert.All(report.Results, r => Assert.Equal(FileOutcome.Copied, r.Outcome));

        var morning = Path.Combine(fx.Destination.Root, "2026-09-27 Sessione 1", "Foto");
        var evening = Path.Combine(fx.Destination.Root, "2026-09-27 Sessione 2", "Video");
        Assert.Equal(ImportFixture.RawBytes, File.ReadAllBytes(Path.Combine(morning, "IMG_0001.CR3")));
        Assert.True(File.Exists(Path.Combine(morning, "IMG_0002.JPG")));
        Assert.True(File.Exists(Path.Combine(evening, "MVI_0003.MP4")));
        Assert.Empty(Directory.GetFiles(fx.Destination.Root, "*" + ImportRunner.PartialSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Copy_keeps_the_original_file_date()
    {
        using var fx = new ImportFixture();
        var options = fx.Options();
        var report = await Runner.RunAsync(fx.Plan(options), options);

        var video = report.Results.Single(r => r.File.Source.Kind == MediaKind.Video);
        Assert.Equal(new DateTime(2026, 9, 27, 21, 0, 0), File.GetLastWriteTime(video.FinalPath!));
    }

    [Theory]
    [InlineData(SubfolderMode.None, "IMG_0001.CR3")]
    [InlineData(SubfolderMode.ByFormat, "RAW/IMG_0001.CR3")]
    public async Task Subfolder_mode_decides_where_files_go(SubfolderMode mode, string relative)
    {
        using var fx = new ImportFixture();
        var options = fx.Options(o => o with { Subfolders = mode });
        await Runner.RunAsync(fx.Plan(options), options);

        Assert.True(File.Exists(Path.Combine(fx.Destination.Root, "2026-09-27 Sessione 1", relative)));
    }

    [Fact]
    public async Task Raw_only_leaves_the_jpegs_out()
    {
        using var fx = new ImportFixture();
        var options = fx.Options(o => o with { Format = new ImportFormat(PhotoFormats.RawOnly, IncludeVideos: false) });
        var report = await Runner.RunAsync(fx.Plan(options), options);

        Assert.Equal(2, report.Count(FileOutcome.Copied));
        Assert.All(report.Results, r => Assert.Equal(MediaKind.Raw, r.File.Source.Kind));
    }

    [Fact]
    public async Task Importing_the_same_card_twice_skips_identical_files()
    {
        using var fx = new ImportFixture();
        var options = fx.Options();
        await Runner.RunAsync(fx.Plan(options), options);

        var second = await Runner.RunAsync(fx.Plan(options), options);

        Assert.All(second.Results, r => Assert.Equal(FileOutcome.SkippedIdentical, r.Outcome));
        Assert.All(second.Results, r => Assert.NotEqual(0UL, r.Hash));
    }

    [Fact]
    public async Task Different_file_with_the_same_name_is_renamed_never_overwritten()
    {
        using var fx = new ImportFixture();
        var options = fx.Options(o => o with { Subfolders = SubfolderMode.None });
        var existing = fx.Destination.Write(Path.Combine("2026-09-27 Sessione 1", "IMG_0001.CR3"), [1, 2, 3]);

        var report = await Runner.RunAsync(fx.Plan(options), options);

        Assert.Equal([1, 2, 3], File.ReadAllBytes(existing));
        var raw = report.Results.Single(r => r.File.Source.FileName == "IMG_0001.CR3");
        Assert.Equal("IMG_0001 (1).CR3", Path.GetFileName(raw.FinalPath));
        Assert.Equal(ImportFixture.RawBytes, File.ReadAllBytes(raw.FinalPath!));
    }

    [Fact]
    public async Task Overwrite_policy_replaces_the_existing_file()
    {
        using var fx = new ImportFixture();
        var options = fx.Options(o => o with { Subfolders = SubfolderMode.None, Duplicates = DuplicatePolicy.Overwrite });
        var existing = fx.Destination.Write(Path.Combine("2026-09-27 Sessione 1", "IMG_0001.CR3"), [1, 2, 3]);

        await Runner.RunAsync(fx.Plan(options), options);

        Assert.Equal(ImportFixture.RawBytes, File.ReadAllBytes(existing));
    }

    [Fact]
    public void Same_name_in_two_camera_folders_gets_a_suffix_in_the_plan()
    {
        using var fx = new ImportFixture(card =>
        {
            card.Write(Path.Combine("DCIM", "100CANON", "IMG_0001.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 07:42:00"));
            card.Write(Path.Combine("DCIM", "101CANON", "IMG_0001.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 07:43:00"));
        });
        var plan = fx.Plan(fx.Options(o => o with { Subfolders = SubfolderMode.None }));

        Assert.Equal(["IMG_0001.JPG", "IMG_0001 (1).JPG"], plan.Files.Select(f => Path.GetFileName(f.TargetPath)));
    }

    [Fact]
    public async Task Card_disconnected_during_the_copy_stops_and_marks_the_rest_not_processed()
    {
        using var fx = new ImportFixture();
        var options = fx.Options();
        var unplug = new SynchronousProgress<CopyProgress>(p =>
        {
            if (p.FilesDone == 1)
                fx.Card.Dispose();
        });

        var report = await Runner.RunAsync(fx.Plan(options), options, unplug);

        Assert.Equal(StopReason.SourceDisconnected, report.StopReason);
        Assert.Equal(1, report.Count(FileOutcome.Copied));
        Assert.Equal(4, report.Count(FileOutcome.NotProcessed));
    }

    [Fact]
    public async Task Cancel_stops_without_leaving_partial_files()
    {
        using var fx = new ImportFixture();
        var options = fx.Options();
        using var cancel = new CancellationTokenSource();
        var progress = new SynchronousProgress<CopyProgress>(p =>
        {
            if (p.FilesDone == 2)
                cancel.Cancel();
        });

        var report = await Runner.RunAsync(fx.Plan(options), options, progress, cancellationToken: cancel.Token);

        Assert.Equal(StopReason.Canceled, report.StopReason);
        Assert.Equal(2, report.Count(FileOutcome.Copied));
        Assert.Empty(Directory.GetFiles(fx.Destination.Root, "*" + ImportRunner.PartialSuffix, SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Failed_files_can_be_retried()
    {
        using var fx = new ImportFixture();
        var options = fx.Options();
        // Un file al posto della cartella "Video" impedisce di copiare il video.
        var blocker = fx.Destination.WriteText(Path.Combine("2026-09-27 Sessione 2", "Video"), "non sono una cartella");

        var first = await Runner.RunAsync(fx.Plan(options), options);
        var failed = Assert.Single(first.Results, r => r.Outcome == FileOutcome.Failed);
        Assert.Equal(FileFailure.ReadOrWrite, failed.Failure);
        Assert.Equal(StopReason.None, first.StopReason);

        File.Delete(blocker);
        var second = await Runner.RetryAsync(first, options);

        Assert.All(second.Results, r => Assert.Equal(FileOutcome.Copied, r.Outcome));
        Assert.Equal(first.Started, second.Started);
    }

    [Fact]
    public async Task Pause_holds_the_copy_until_resumed()
    {
        using var fx = new ImportFixture();
        var options = fx.Options();
        var gate = new PauseGate();
        gate.Pause();

        var run = Runner.RunAsync(fx.Plan(options), options, pause: gate);
        await Task.Delay(200);
        Assert.False(run.IsCompleted);

        gate.Resume();
        var report = await run.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(5, report.Count(FileOutcome.Copied));
    }

    [Fact]
    public async Task Report_is_saved_as_csv_in_the_app_data_folder_never_in_the_destination()
    {
        using var fx = new ImportFixture();
        using var appData = new TempSource();
        var options = fx.Options();
        var report = await Runner.RunAsync(fx.Plan(options), options);

        var path = ReportWriter.Save(report, new AppDataFolder(appData.Root).Reports);

        Assert.StartsWith(Path.Combine(appData.Root, "reports"), path);
        var lines = File.ReadAllLines(path);
        Assert.Equal(6, lines.Length);
        Assert.Contains("Copied", lines[1]);

        // Nella destinazione ci sono solo le foto: nessun file o cartella dell'app.
        Assert.All(Directory.GetFiles(fx.Destination.Root, "*", SearchOption.AllDirectories),
            f => Assert.NotNull(MediaKinds.FromPath(f)));
    }
}

/// <summary>A differenza di <see cref="Progress{T}"/> chiama subito il callback, sullo stesso thread.</summary>
internal sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
