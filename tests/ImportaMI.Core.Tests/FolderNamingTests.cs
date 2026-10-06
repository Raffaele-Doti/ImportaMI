using ImportaMI.Core.Import;
using ImportaMI.Core.Sessions;
using static ImportaMI.Core.Tests.TestData;

namespace ImportaMI.Core.Tests;

public class FolderNamingTests
{
    [Theory]
    [InlineData("Alba al lago di Como", "Alba al lago di Como")]
    [InlineData("Matrimonio Bianchi: cerimonia", "Matrimonio Bianchi_ cerimonia")]
    [InlineData("Foto/Video?", "Foto_Video_")]
    [InlineData("  Ritratti studio...  ", "Ritratti studio")]
    [InlineData("Tab\there", "Tab_here")]
    [InlineData("", "")]
    public void Sanitize_produces_a_portable_folder_name(string input, string expected)
    {
        Assert.Equal(expected, FolderNaming.Sanitize(input));
    }

    [Theory]
    [InlineData(FolderLayout.DateAndName, new[] { "2026-09-27 Alba al lago" })]
    [InlineData(FolderLayout.YearThenDateAndName, new[] { "2026", "2026-09-27 Alba al lago" })]
    [InlineData(FolderLayout.YearMonthThenDateAndName, new[] { "2026", "09", "2026-09-27 Alba al lago" })]
    public void Builds_segments_for_each_layout(FolderLayout layout, string[] expected)
    {
        var segments = FolderNaming.RelativeSegments(At("2026-09-27 07:42:00"), "Alba al lago", layout);

        Assert.Equal(expected, segments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void Empty_name_falls_back_to_the_date(string name)
    {
        var segments = FolderNaming.RelativeSegments(At("2026-10-04 08:15:00"), name, FolderLayout.DateAndName);

        Assert.Equal(["2026-10-04"], segments);
    }
}
