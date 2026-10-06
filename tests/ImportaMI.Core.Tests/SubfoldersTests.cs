using ImportaMI.Core.Import;
using ImportaMI.Core.Media;

namespace ImportaMI.Core.Tests;

public class SubfoldersTests
{
    private static readonly SubfolderNames Italian = new("Foto", "RAW", "JPG", "Video");

    [Theory]
    [InlineData(MediaKind.Raw, null)]
    [InlineData(MediaKind.Video, null)]
    public void None_keeps_everything_in_the_session_folder(MediaKind kind, string? expected)
    {
        Assert.Equal(expected, Subfolders.For(kind, SubfolderMode.None, Italian));
    }

    [Theory]
    [InlineData(MediaKind.Raw, "Foto")]
    [InlineData(MediaKind.Jpeg, "Foto")]
    [InlineData(MediaKind.Video, "Video")]
    public void Photos_and_videos_are_split_in_two(MediaKind kind, string expected)
    {
        Assert.Equal(expected, Subfolders.For(kind, SubfolderMode.PhotosAndVideos, Italian));
    }

    [Theory]
    [InlineData(MediaKind.Raw, "RAW")]
    [InlineData(MediaKind.Jpeg, "JPG")]
    [InlineData(MediaKind.Video, "Video")]
    public void By_format_uses_one_folder_per_type(MediaKind kind, string expected)
    {
        Assert.Equal(expected, Subfolders.For(kind, SubfolderMode.ByFormat, Italian));
    }
}
