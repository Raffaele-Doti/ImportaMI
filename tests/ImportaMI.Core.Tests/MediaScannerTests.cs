using ImportaMI.Core.Media;
using ImportaMI.Core.Scanning;
using static ImportaMI.Core.Tests.TestData;

namespace ImportaMI.Core.Tests;

public class MediaScannerTests
{
    private static readonly MediaScanner Scanner = new(new MetadataCaptureTimeReader());

    [Fact]
    public void Reads_the_exif_capture_time_of_a_real_jpeg()
    {
        using var source = new TempSource();
        source.Write(Path.Combine("DCIM", "100EOSR6", "IMG_4012.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 07:42:10"));

        var result = Scanner.Scan(source.Root);

        var file = Assert.Single(result.Files);
        Assert.Equal(At("2026-09-27 07:42:10"), file.CaptureTime);
        Assert.Equal(CaptureTimeSource.Metadata, file.TimeSource);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Empty_usb_stick_has_no_media_and_no_other_files()
    {
        using var source = new TempSource();

        var result = Scanner.Scan(source.Root);

        Assert.False(result.HasMedia);
        Assert.Equal(0, result.OtherFiles);
    }

    [Fact]
    public void Stick_with_only_documents_reports_them_as_other_files()
    {
        using var source = new TempSource();
        source.WriteText("Tesi.docx", "documento");
        source.WriteText(Path.Combine("Musica", "brano.mp3"), "audio");
        source.WriteText(Path.Combine("MISC", "CANONMSC.CTG"), "indice della fotocamera");

        var result = Scanner.Scan(source.Root);

        Assert.False(result.HasMedia);
        Assert.Equal(3, result.OtherFiles);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Corrupt_photo_falls_back_to_the_file_date_and_is_reported()
    {
        using var source = new TempSource();
        var path = source.Write("IMG_0001.JPG", [0x01, 0x02, 0x03, 0x04, 0x05]);
        var fileDate = new DateTime(2026, 9, 27, 8, 0, 0);
        File.SetLastWriteTime(path, fileDate);

        var result = Scanner.Scan(source.Root);

        var file = Assert.Single(result.Files);
        Assert.Equal(CaptureTimeSource.FileDate, file.TimeSource);
        Assert.Equal(fileDate, file.CaptureTime);
        Assert.Equal(ScanIssueKind.MetadataUnreadable, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public void Empty_file_is_skipped_and_reported()
    {
        using var source = new TempSource();
        source.Write("IMG_0002.CR3", []);

        var result = Scanner.Scan(source.Root);

        Assert.Empty(result.Files);
        Assert.Equal(ScanIssueKind.EmptyFile, Assert.Single(result.Issues).Kind);
    }

    [Fact]
    public void Mac_finder_metadata_files_are_not_photos()
    {
        using var source = new TempSource();
        source.Write("IMG_0003.JPG", TestJpeg.WithDateTimeOriginal("2026:09:27 09:00:00"));
        source.Write("._IMG_0003.JPG", [0x00, 0x05, 0x16, 0x07]);

        var result = Scanner.Scan(source.Root);

        Assert.Single(result.Files);
        Assert.Equal(1, result.OtherFiles);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void System_folders_are_not_scanned()
    {
        using var source = new TempSource();
        source.Write(Path.Combine("$RECYCLE.BIN", "IMG_0004.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 09:00:00"));
        source.Write(Path.Combine(".Trashes", "501", "IMG_0005.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 09:00:00"));
        source.Write(Path.Combine("DCIM", "IMG_0006.JPG"), TestJpeg.WithDateTimeOriginal("2026:09:27 09:00:00"));

        var result = Scanner.Scan(source.Root);

        Assert.Equal("IMG_0006.JPG", Assert.Single(result.Files).FileName);
    }

    [Fact]
    public void Missing_source_raises_a_dedicated_error()
    {
        var missing = Path.Combine(Path.GetTempPath(), "ImportaMI-tests", "scheda-scollegata-" + Guid.NewGuid().ToString("N"));

        var error = Assert.Throws<SourceNotAvailableException>(() => Scanner.Scan(missing));
        Assert.Equal(missing, error.SourcePath);
    }

    [Fact]
    public void Card_removed_during_the_scan_stops_it_instead_of_listing_every_file()
    {
        var source = new TempSource();
        for (var i = 0; i < 5; i++)
            source.Write($"IMG_{i:0000}.JPG", TestJpeg.WithDateTimeOriginal("2026:09:27 09:00:00"));
        var scanner = new MediaScanner(new UnplugOnFirstRead(source));

        Assert.Throws<SourceNotAvailableException>(() => scanner.Scan(source.Root));
    }

    /// <summary>Al primo file "scollega" la scheda cancellandone la cartella, poi fallisce come farebbe il disco.</summary>
    private sealed class UnplugOnFirstRead(TempSource source) : ICaptureTimeReader
    {
        public CaptureTimeResult Read(string path, MediaKind kind)
        {
            source.Dispose();
            throw new IOException("Il dispositivo non è pronto.");
        }
    }

    [Fact]
    public void Scan_can_be_cancelled()
    {
        using var source = new TempSource();
        source.Write("IMG_0007.JPG", TestJpeg.WithDateTimeOriginal("2026:09:27 09:00:00"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => Scanner.Scan(source.Root, cancellationToken: cancellation.Token));
    }

    [Fact]
    public void Reports_progress_with_final_counts()
    {
        using var source = new TempSource();
        source.Write("IMG_0008.JPG", TestJpeg.WithDateTimeOriginal("2026:09:27 09:00:00"));
        source.WriteText("note.txt", "testo");
        var reports = new List<ScanProgress>();

        Scanner.Scan(source.Root, new SynchronousProgress<ScanProgress>(reports.Add));

        Assert.Equal(new ScanProgress(2, 1, source.Root), reports[^1]);
    }

    [Fact]
    public void Shot_prefers_the_metadata_time_over_a_file_date()
    {
        var raw = FileAt("IMG_4012.CR3", "2026-09-27 12:00:00") with { TimeSource = CaptureTimeSource.FileDate };
        var jpeg = FileAt("IMG_4012.JPG", "2026-09-27 07:42:10");

        var shot = new Shot([raw, jpeg]);

        Assert.Equal(At("2026-09-27 07:42:10"), shot.CaptureTime);
        Assert.Equal(CaptureTimeSource.Metadata, shot.TimeSource);
    }
}
