using ImportaMI.Core.Media;
using static ImportaMI.Core.Tests.TestData;

namespace ImportaMI.Core.Tests;

public class ShotBuilderTests
{
    [Fact]
    public void Pairs_raw_and_jpeg_with_the_same_name()
    {
        var shots = ShotBuilder.Build(
        [
            FileAt("IMG_4012.CR3", "2026-09-27 07:42:00"),
            FileAt("img_4012.jpg", "2026-09-27 07:42:00", size: 8_000_000),
            FileAt("IMG_4013.CR3", "2026-09-27 07:42:03"),
        ]);

        Assert.Equal(2, shots.Count);
        Assert.True(shots[0].HasRaw);
        Assert.True(shots[0].HasJpeg);
        Assert.Equal(33_000_000, shots[0].SizeBytes);
        Assert.False(shots[1].HasJpeg);
    }

    [Fact]
    public void Does_not_pair_files_from_different_folders()
    {
        var otherFolder = Path.Combine("E:", "DCIM", "101EOSR6");

        var shots = ShotBuilder.Build(
        [
            FileAt("IMG_0001.CR3", "2026-09-27 07:42:00"),
            FileAt("IMG_0001.JPG", "2026-10-04 08:15:00", folder: otherFolder),
        ]);

        Assert.Equal(2, shots.Count);
    }

    [Fact]
    public void Video_is_never_paired_with_a_photo()
    {
        var shots = ShotBuilder.Build(
        [
            FileAt("C0001.MP4", "2026-10-02 21:10:00"),
            FileAt("C0001.JPG", "2026-10-02 21:10:00"),
        ]);

        Assert.Equal(2, shots.Count);
        Assert.Single(shots, s => s.IsVideo);
    }

    [Fact]
    public void Shots_are_sorted_by_capture_time()
    {
        var shots = ShotBuilder.Build(
        [
            FileAt("IMG_0002.CR3", "2026-09-27 08:00:00"),
            FileAt("IMG_0001.CR3", "2026-09-27 07:00:00"),
        ]);

        Assert.Equal("IMG_0001.CR3", shots[0].Files[0].FileName);
    }

    [Theory]
    [InlineData("IMG_1.CR3", MediaKind.Raw)]
    [InlineData("DSC_1.nef", MediaKind.Raw)]
    [InlineData("IMG_1.HIF", MediaKind.Jpeg)]
    [InlineData("IMG_1.jpeg", MediaKind.Jpeg)]
    [InlineData("MVI_1.MOV", MediaKind.Video)]
    [InlineData("CANONMSC.CTG", null)]
    [InlineData("README", null)]
    public void Recognizes_supported_extensions(string name, MediaKind? expected)
    {
        Assert.Equal(expected, MediaKinds.FromPath(name));
    }
}
