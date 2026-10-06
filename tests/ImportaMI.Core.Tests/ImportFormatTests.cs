using ImportaMI.Core.Import;
using ImportaMI.Core.Media;
using static ImportaMI.Core.Tests.TestData;

namespace ImportaMI.Core.Tests;

public class ImportFormatTests
{
    private static readonly Shot RawPlusJpeg = new(
    [
        FileAt("IMG_4012.CR3", "2026-09-27 07:42:00"),
        FileAt("IMG_4012.JPG", "2026-09-27 07:42:00"),
    ]);

    private static readonly Shot RawOnlyShot = new([FileAt("IMG_4013.CR3", "2026-09-27 07:42:05")]);

    private static readonly Shot Video = new([FileAt("MVI_4100.MP4", "2026-10-02 21:10:00")]);

    [Theory]
    [InlineData(PhotoFormats.RawAndJpeg, new[] { "IMG_4012.CR3", "IMG_4012.JPG" })]
    [InlineData(PhotoFormats.RawOnly, new[] { "IMG_4012.CR3" })]
    [InlineData(PhotoFormats.JpegOnly, new[] { "IMG_4012.JPG" })]
    public void Selects_files_by_photo_format(PhotoFormats photos, string[] expected)
    {
        var format = new ImportFormat(photos, IncludeVideos: true);

        Assert.Equal(expected, format.FilesToImport(RawPlusJpeg).Select(f => f.FileName));
    }

    [Fact]
    public void Jpeg_only_skips_shots_without_a_jpeg()
    {
        var format = new ImportFormat(PhotoFormats.JpegOnly, IncludeVideos: true);

        Assert.Empty(format.FilesToImport(RawOnlyShot));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Videos_follow_their_own_switch(bool includeVideos, int expected)
    {
        var format = new ImportFormat(PhotoFormats.RawOnly, includeVideos);

        Assert.Equal(expected, format.FilesToImport(Video).Count());
    }
}
