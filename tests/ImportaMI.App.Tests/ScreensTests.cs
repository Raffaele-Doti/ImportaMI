using System.Diagnostics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ImportaMI.App.Localization;
using ImportaMI.App.ViewModels;
using ImportaMI.App.Views;
using ImportaMI.Core;
using ImportaMI.Core.Settings;
using ImportaMI.Core.Tests;

namespace ImportaMI.App.Tests;

/// <summary>
/// Percorre l'app con una scheda simulata e salva uno screenshot di ogni schermata
/// in <c>bin/…/screenshots</c>. I test stanno in una sola classe perché cambiano la lingua,
/// che è globale.
/// </summary>
public class ScreensTests
{
    private static readonly string ScreenshotsDir = Path.Combine(AppContext.BaseDirectory, "screenshots");

    [AvaloniaTheory]
    [InlineData("it")]
    [InlineData("en")]
    public void Card_with_photos_is_grouped_into_sessions(string language)
    {
        using var card = DemoCard.Create();
        var (window, main) = Open(card.Root, language);
        var source = Assert.IsType<SourceViewModel>(main.CurrentPage);

        ChooseFolderAndScan(source);
        WaitUntil(() => main.CurrentPage is SessionsViewModel);

        var sessions = Assert.IsType<SessionsViewModel>(main.CurrentPage);
        WaitUntil(() => !sessions.IsCheckingArchive);

        // All'inizio una sessione per giorno: 27 set, 2 ott, 3 ott (il concerto supera la mezzanotte).
        Assert.Equal([6, 2, 2], sessions.Sessions.Select(s => s.Session.Shots.Count));
        var wedding = language == "it" ? "Matrimonio Bianchi" : "Bianchi wedding";
        sessions.Sessions[0].Name = wedding;

        // Il 27 settembre contiene due servizi: si divide cliccando la prima foto del carnevale (15:30).
        sessions.Sessions[0].SplitCommand.Execute(null);
        var splitter = Assert.IsType<SplitEditorViewModel>(sessions.Splitter);
        WaitUntil(() => splitter.Tiles.All(t => t.Thumbnail.IsLoaded));
        splitter.Tiles[3].ToggleCommand.Execute(null);
        Save(window, $"2-split-{language}");
        splitter.ApplyCommand.Execute(null);

        Assert.Null(sessions.Splitter);
        Assert.Equal([3, 3, 2, 2], sessions.Sessions.Select(s => s.Session.Shots.Count));
        Assert.Equal(wedding, sessions.Sessions[0].Name);
        sessions.Sessions[1].Name = language == "it" ? "Carnevale: sfilata" : "Carnival: parade";
        Assert.NotNull(sessions.Sessions[1].Warning); // i due punti non sono ammessi nei nomi di cartella

        // Il concerto dopo mezzanotte va unito alla sessione del 2 ottobre.
        sessions.Sessions[3].MergeCommand.Execute(null);
        Assert.Equal([3, 3, 4], sessions.Sessions.Select(s => s.Session.Shots.Count));
        Assert.Equal(wedding, sessions.Sessions[0].Name);
        Save(window, $"2-sessions-{language}");
    }

    [AvaloniaFact]
    public void Second_import_of_the_same_card_finds_the_photos_in_the_archive()
    {
        using var card = DemoCard.Create();
        using var destination = new TempSource();
        var (window, main) = Open(card.Root, "it");

        // Prima importazione, senza pulizia della scheda.
        var source = (SourceViewModel)main.CurrentPage;
        ChooseFolderAndScan(source);
        WaitUntil(() => main.CurrentPage is SessionsViewModel s && !s.IsCheckingArchive);
        ((SessionsViewModel)main.CurrentPage).NextCommand.Execute(null);
        var target = (DestinationViewModel)main.CurrentPage;
        target.DestinationPath = destination.Root;
        target.StartCommand.Execute(null);
        var copy = (CopyViewModel)main.CurrentPage;
        WaitUntil(() => copy.IsFinished);
        copy.NewImportCommand.Execute(null);

        // Torna al passo 1 con la scheda selezionata e l'avviso dei file rimasti.
        Assert.Same(source, main.CurrentPage);
        Assert.True(source.HasMessage);
        Assert.Contains("19", source.MessageTitle);

        // Seconda scansione: tutto è già nell'archivio, quindi niente è selezionato.
        source.ScanCommand.Execute(null);
        WaitUntil(() => main.CurrentPage is SessionsViewModel s && !s.IsCheckingArchive);
        var sessions = (SessionsViewModel)main.CurrentPage;
        Assert.All(sessions.Sessions, s => Assert.True(s.IsFullyArchived));
        Assert.All(sessions.Sessions, s => Assert.False(s.IsSelected));
        Assert.False(sessions.NextCommand.CanExecute(null));
        Save(window, "2-sessions-already-imported-it");
    }

    [AvaloniaFact]
    public void Usb_stick_with_only_documents_explains_that_there_is_nothing_to_import()
    {
        using var stick = new TempSource();
        stick.WriteText("Tesi di laurea.docx", "documento");
        stick.WriteText(Path.Combine("Musica", "brano.mp3"), "audio");
        var (window, main) = Open(stick.Root, "it");
        var source = Assert.IsType<SourceViewModel>(main.CurrentPage);

        ChooseFolderAndScan(source);
        WaitUntil(() => source.HasMessage);

        Assert.IsType<SourceViewModel>(main.CurrentPage);
        Assert.Contains("2", source.MessageDetail);
        Save(window, "1-source-nothing-found-it");
    }

    [AvaloniaFact]
    public void Disconnected_folder_shows_a_clear_message()
    {
        var stick = new TempSource();
        var (_, main) = Open(stick.Root, "it");
        var source = Assert.IsType<SourceViewModel>(main.CurrentPage);
        source.BrowseCommand.Execute(null);
        WaitUntil(() => source.SelectedSource is { IsFolder: true });

        stick.Dispose(); // la "chiavetta" sparisce prima della scansione
        source.ScanCommand.Execute(null);
        WaitUntil(() => source.HasMessage);

        Assert.True(source.MessageIsError);
        Assert.Null(source.SelectedSource);
    }

    [AvaloniaTheory]
    [InlineData("it")]
    [InlineData("en")]
    public void Full_import_copies_verifies_and_cleans_the_card(string language)
    {
        using var card = DemoCard.Create();
        using var destination = new TempSource();
        var (window, main) = Open(card.Root, language);
        var source = Assert.IsType<SourceViewModel>(main.CurrentPage);
        ChooseFolderAndScan(source);
        WaitUntil(() => main.CurrentPage is SessionsViewModel);
        var sessions = (SessionsViewModel)main.CurrentPage;
        sessions.Sessions[0].Name = language == "it" ? "Alba al lago di Como" : "Sunrise at Lake Como";
        WaitUntil(() => sessions.Sessions.SelectMany(s => s.Thumbnails).Where(t => !t.IsVideo).All(t => t.HasImage));
        Save(window, $"2-sessions-{language}");

        // 3 · Destinazione
        sessions.NextCommand.Execute(null);
        var target = Assert.IsType<DestinationViewModel>(main.CurrentPage);
        target.DestinationPath = destination.Root;
        Assert.Null(target.Error);
        Assert.True(target.CanStart);
        Save(window, $"3-destination-{language}");

        // La destinazione sulla scheda stessa viene rifiutata.
        target.DestinationPath = Path.Combine(card.Root, "Foto");
        Assert.NotNull(target.Error);
        Assert.False(target.CanStart);
        target.DestinationPath = destination.Root;

        // 4 · Copia e report
        target.StartCommand.Execute(null);
        var copy = Assert.IsType<CopyViewModel>(main.CurrentPage);
        WaitUntil(() => copy.IsFinished);
        Assert.Null(copy.StopMessage);
        // 9 JPEG veri; il file vuoto e il "._" di macOS non sono foto da copiare.
        Assert.Equal(9, Directory.GetFiles(destination.Root, "*.JPG", SearchOption.AllDirectories).Length);
        Assert.NotNull(copy.ReportPath);
        Assert.StartsWith(DataFolder.Reports, copy.ReportPath);
        Assert.Empty(Directory.GetDirectories(destination.Root, ".importami", SearchOption.AllDirectories));
        Save(window, $"4-report-{language}");

        // 5 · Pulizia: elimina dalla scheda solo ciò che è stato copiato e verificato.
        copy.CleanupCommand.Execute(null);
        var cleanup = Assert.IsType<CleanupViewModel>(main.CurrentPage);
        Assert.False(cleanup.DeleteCommand.CanExecute(null)); // serve la conferma
        Save(window, $"5-cleanup-{language}");
        cleanup.Acknowledged = true;
        cleanup.DeleteCommand.Execute(null);
        WaitUntil(() => cleanup.IsDone);
        Save(window, $"5-cleanup-done-{language}");

        Assert.Empty(card.Files("IMG_40??.CR3"));
        Assert.Single(card.Files("IMG_4031.JPG"));     // il file vuoto non era copiabile: resta
        Assert.True(File.Exists(Path.Combine(card.Root, "MISC", "CANONMSC.CTG")));
        Assert.True(Directory.GetFiles(destination.Root, "*.CR3", SearchOption.AllDirectories).Length == 9);

        // Le scelte fatte vengono ricordate per la volta successiva.
        Assert.Equal(destination.Root, Settings.Load().DestinationRoot);
    }

    private static SettingsStore Settings = null!;

    private static AppDataFolder DataFolder = null!;

    private static (MainWindow Window, MainViewModel Main) Open(string folder, string language)
    {
        AppLanguage.Apply(language);
        // Impostazioni in una cartella temporanea: i test non devono toccare quelle dell'utente.
        var settingsFolder = Path.Combine(Path.GetTempPath(), "ImportaMI-tests", Guid.NewGuid().ToString("N"));
        Settings = new SettingsStore(Path.Combine(settingsFolder, "settings.json"));
        // Archivio iniziale vuoto in una cartella temporanea, invece della cartella Immagini dell'utente.
        Settings.Save(new ImportSettings { DestinationRoot = Path.Combine(settingsFolder, "archivio") });
        DataFolder = new AppDataFolder(settingsFolder);
        var main = new MainViewModel(new FixedFolderPicker(folder), Settings, eject: _ => Task.FromResult(true), DataFolder);
        var window = new MainWindow { Width = 1280, Height = 820, DataContext = main };
        window.Show();
        return (window, main);
    }

    private static void ChooseFolderAndScan(SourceViewModel source)
    {
        source.BrowseCommand.Execute(null);
        WaitUntil(() => source.SelectedSource is { IsFolder: true });
        source.ScanCommand.Execute(null);
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(15))
                throw new TimeoutException("La condizione attesa non si è verificata.");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(MainWindow window, string name)
    {
        Directory.CreateDirectory(ScreenshotsDir);
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nessun frame disponibile.");
        frame.Save(Path.Combine(ScreenshotsDir, name + ".png"));
    }
}

/// <summary>
/// Una scheda simulata con lo stesso esempio del README, più i casi scomodi di una scheda vera:
/// RAW che la libreria non sa leggere, un video senza metadati, un file vuoto, file di sistema.
/// </summary>
internal static class DemoCard
{
    public static TempSource Create()
    {
        var card = new TempSource();
        var dcim = Path.Combine("DCIM", "100CANON");
        string[] times =
        [
            "2026:09:27 07:42:00", "2026:09:27 08:04:00", "2026:09:27 08:29:00",
            "2026:09:27 15:30:00", "2026:09:27 16:05:00",
            "2026:09:27 17:20:00",
            "2026:10:02 23:35:00", "2026:10:02 23:58:00", "2026:10:03 00:20:00",
        ];

        for (var i = 0; i < times.Length; i++)
        {
            var name = $"IMG_{4012 + i}";
            card.Write(Path.Combine(dcim, name + ".JPG"), RealJpeg(times[i], hue: i * 40));
            // RAW "illeggibile": lo scatto prende comunque l'orario dal JPEG gemello.
            card.Write(Path.Combine(dcim, name + ".CR3"), [.. Enumerable.Repeat((byte)0x42, 2048)]);
        }

        // Video senza metadati leggibili: viene datato con la data del file.
        var video = card.Write(Path.Combine(dcim, "MVI_4030.MP4"), [.. Enumerable.Repeat((byte)0x00, 4096)]);
        File.SetLastWriteTime(video, new DateTime(2026, 10, 3, 0, 5, 0));

        card.Write(Path.Combine(dcim, "IMG_4031.JPG"), []);                // scrittura interrotta
        card.Write(Path.Combine(dcim, "._IMG_4012.JPG"), [0x00, 0x05]);     // metadati del Finder di macOS
        card.WriteText(Path.Combine("MISC", "CANONMSC.CTG"), "indice");    // file della fotocamera
        return card;
    }

    /// <summary>
    /// Un JPEG vero (un cielo sfumato su un prato) con la data EXIF: il segmento APP1 del JPEG minimo
    /// dei test di Core viene inserito subito dopo l'inizio del file codificato da Skia.
    /// </summary>
    private static byte[] RealJpeg(string exifDate, int hue)
    {
        using var bitmap = new SkiaSharp.SKBitmap(600, 400);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            using var sky = new SkiaSharp.SKPaint
            {
                Shader = SkiaSharp.SKShader.CreateLinearGradient(
                    new SkiaSharp.SKPoint(0, 0), new SkiaSharp.SKPoint(0, 260),
                    [SkiaSharp.SKColor.FromHsl(hue % 360, 60, 35), SkiaSharp.SKColor.FromHsl((hue + 30) % 360, 70, 70)],
                    SkiaSharp.SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(0, 0, 600, 260, sky);
            using var ground = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.FromHsl((hue + 140) % 360, 30, 22) };
            canvas.DrawRect(0, 260, 600, 140, ground);
            using var sun = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.FromHsl((hue + 45) % 360, 90, 85), IsAntialias = true };
            canvas.DrawCircle(150 + (hue % 300), 200, 34, sun);
        }

        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 80).ToArray();
        var exif = TestJpeg.WithDateTimeOriginal(exifDate);
        var app1 = exif[2..^2]; // senza SOI iniziale e EOI finale
        return [0xFF, 0xD8, .. app1, .. encoded[2..]];
    }
}
