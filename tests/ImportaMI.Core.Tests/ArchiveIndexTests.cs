using ImportaMI.Core.Archive;
using ImportaMI.Core.Import;
using ImportaMI.Core.Media;
using ImportaMI.Core.Sessions;

namespace ImportaMI.Core.Tests;

public class ArchiveIndexTests
{
    private static IReadOnlyList<MediaFile> CardFiles(ImportFixture fx) =>
        fx.Sessions.SelectMany(s => s.Shots).SelectMany(s => s.Files).ToList();

    [Fact]
    public void Finds_a_photo_already_archived_under_another_folder()
    {
        using var fx = new ImportFixture();
        var archived = fx.Destination.Write(Path.Combine("2025", "Vecchia importazione", "IMG_0001.CR3"), ImportFixture.RawBytes);

        var copies = ArchiveIndex.Build(fx.Destination.Root).FindCopies(CardFiles(fx));

        var source = CardFiles(fx).Single(f => f.FileName == "IMG_0001.CR3").SourcePath;
        Assert.Equal(archived, copies[source]);
        // Nella scheda di prova anche IMG_0002.CR3 ha esattamente lo stesso contenuto: il confronto è sul contenuto.
        Assert.Equal(2, copies.Count);
        Assert.DoesNotContain(CardFiles(fx).Where(f => f.Kind != MediaKind.Raw), f => copies.ContainsKey(f.SourcePath));
    }

    [Fact]
    public void Same_name_but_different_content_or_size_is_not_a_copy()
    {
        using var fx = new ImportFixture();
        var sameSize = ImportFixture.RawBytes.ToArray();
        sameSize[0] ^= 0xFF;
        fx.Destination.Write(Path.Combine("A", "IMG_0001.CR3"), sameSize);
        fx.Destination.Write(Path.Combine("B", "IMG_0002.CR3"), [1, 2, 3]);

        var copies = ArchiveIndex.Build(fx.Destination.Root).FindCopies(CardFiles(fx));

        Assert.Empty(copies);
    }

    [Fact]
    public void Finds_a_photo_that_was_renamed_in_the_archive()
    {
        using var fx = new ImportFixture();
        var renamed = fx.Destination.Write(Path.Combine("Clienti", "Bianchi", "sposi-001.cr3"), ImportFixture.RawBytes);

        var copies = ArchiveIndex.Build(fx.Destination.Root).FindCopies(CardFiles(fx));

        // Stesso contenuto dei due RAW della scheda: entrambi vengono riconosciuti nel file rinominato.
        Assert.Equal(2, copies.Count);
        Assert.All(copies.Values, v => Assert.Equal(renamed, v));
    }

    [Fact]
    public void Photos_with_the_same_name_and_size_but_different_content_are_different()
    {
        using var fx = new ImportFixture();
        // Differisce solo nell'ultimo byte: nome e dimensione uguali non bastano.
        var almost = ImportFixture.RawBytes.ToArray();
        almost[^1] ^= 0xFF;
        fx.Destination.Write(Path.Combine("2025", "IMG_0001.CR3"), almost);

        var copies = ArchiveIndex.Build(fx.Destination.Root).FindCopies(CardFiles(fx));

        Assert.Empty(copies);
    }

    [Fact]
    public void A_source_inside_the_archive_does_not_find_itself()
    {
        using var fx = new ImportFixture();

        // L'archivio contiene la "scheda" stessa: ogni file sarebbe la copia di sé stesso.
        // (I due RAW di prova sono esclusi perché hanno davvero lo stesso contenuto tra loro.)
        var unique = CardFiles(fx).Where(f => f.Kind != MediaKind.Raw).ToList();

        var copies = ArchiveIndex.Build(fx.Card.Root).FindCopies(unique);

        Assert.Empty(copies);
    }

    [Fact]
    public void Archive_that_does_not_exist_yet_is_empty()
    {
        var index = ArchiveIndex.Build(Path.Combine(Path.GetTempPath(), "ImportaMI-tests", "archivio-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(0, index.FileCount);
    }

    [Fact]
    public async Task Reimporting_with_a_different_session_name_does_not_duplicate_the_photos()
    {
        using var fx = new ImportFixture();
        var runner = new ImportRunner();
        var options = fx.Options();
        await runner.RunAsync(fx.Plan(options), options);

        // Seconda volta: stesse foto, ma la sessione si chiama diversamente → cartella diversa.
        var copies = ArchiveIndex.Build(fx.Destination.Root).FindCopies(CardFiles(fx));
        var renamed = fx.Sessions.Select((s, i) => new SessionChoice(s, $"Nome nuovo {i}")).ToList();
        var plan = ImportPlanner.Create(fx.Card.Root, renamed, options, copies);
        var second = await runner.RunAsync(plan, options);

        Assert.Equal(plan.Files.Count, plan.AlreadyInArchive);
        Assert.Equal(0, plan.BytesToCopy);
        Assert.All(second.Results, r => Assert.Equal(FileOutcome.SkippedIdentical, r.Outcome));
        Assert.All(second.Results, r => Assert.Contains("Sessione", r.FinalPath));
        Assert.False(Directory.Exists(Path.Combine(fx.Destination.Root, "2026-09-27 Nome nuovo 0")));
    }

    [Fact]
    public async Task Archive_duplicates_are_copied_when_the_option_is_off()
    {
        using var fx = new ImportFixture();
        var runner = new ImportRunner();
        var options = fx.Options();
        await runner.RunAsync(fx.Plan(options), options);

        var copies = ArchiveIndex.Build(fx.Destination.Root).FindCopies(CardFiles(fx));
        var keepAll = options with { SkipArchiveDuplicates = false };
        var renamed = fx.Sessions.Select((s, i) => new SessionChoice(s, $"Nome nuovo {i}")).ToList();
        var second = await runner.RunAsync(ImportPlanner.Create(fx.Card.Root, renamed, keepAll, copies), keepAll);

        Assert.All(second.Results, r => Assert.Equal(FileOutcome.Copied, r.Outcome));
    }

    [Fact]
    public async Task Archive_copy_that_differs_after_the_first_bytes_is_not_trusted()
    {
        using var fx = new ImportFixture();
        var options = fx.Options(o => o with { Subfolders = SubfolderMode.None });
        // Stessa dimensione e stesso inizio, ma diverso alla fine: il controllo rapido lo accetta,
        // quello completo durante la copia no.
        var bytes = ImportFixture.RawBytes.ToArray();
        bytes[^1] ^= 0xFF;
        var lookalike = fx.Destination.Write(Path.Combine("Altro", "IMG_0001.CR3"), bytes);
        var source = CardFiles(fx).Single(f => f.FileName == "IMG_0001.CR3").SourcePath;
        var plan = ImportPlanner.Create(fx.Card.Root,
            fx.Sessions.Select((s, i) => new SessionChoice(s, $"Sessione {i + 1}")).ToList(),
            options,
            new Dictionary<string, string> { [source] = lookalike });

        var report = await new ImportRunner().RunAsync(plan, options);

        var raw = report.Results.Single(r => r.File.Source.SourcePath == source);
        Assert.Equal(FileOutcome.Copied, raw.Outcome);
        Assert.NotEqual(lookalike, raw.FinalPath);
    }
}
