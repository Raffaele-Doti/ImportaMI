using System.Xml.Linq;
using ImportaMI.Core.Localization;

namespace ImportaMI.Core.Tests;

public class LocalizationTests
{
    [Theory]
    [InlineData("it", "en-US", "it")]
    [InlineData("en", "it-IT", "en")]
    [InlineData("EN-gb", null, "en")]
    [InlineData(null, "it-IT", "it")]
    [InlineData("", "it_CH", "it")]
    [InlineData("de", "it-IT", "it")] // lingua non tradotta: si passa a quella di sistema
    [InlineData(null, "fr-FR", "en")] // nessuna delle due tradotta: inglese
    [InlineData(null, null, "en")]
    public void Resolves_the_interface_language(string? preferred, string? system, string expected)
    {
        Assert.Equal(expected, LanguageSelection.Resolve(preferred, system));
    }

    [Fact]
    public void Every_translation_has_the_same_keys_as_english()
    {
        var english = Keys(Path.Combine(ResourcesDir, "Strings.resx"));
        var translations = Directory.GetFiles(ResourcesDir, "Strings.*.resx");

        Assert.NotEmpty(translations);
        foreach (var file in translations)
        {
            var keys = Keys(file);
            Assert.True(english.SetEquals(keys),
                $"{Path.GetFileName(file)}: mancano [{string.Join(", ", english.Except(keys))}], " +
                $"in più [{string.Join(", ", keys.Except(english))}]");
        }
    }

    [Fact]
    public void Every_language_listed_in_code_has_a_resource_file()
    {
        foreach (var language in LanguageSelection.Supported.Where(l => l != LanguageSelection.Fallback))
            Assert.True(File.Exists(Path.Combine(ResourcesDir, $"Strings.{language}.resx")), $"Manca Strings.{language}.resx");
    }

    [Fact]
    public void No_text_is_empty()
    {
        foreach (var file in Directory.GetFiles(ResourcesDir, "Strings*.resx"))
        {
            var empty = XDocument.Load(file).Root!.Elements("data")
                .Where(d => string.IsNullOrWhiteSpace(d.Element("value")?.Value))
                .Select(d => d.Attribute("name")!.Value);
            Assert.Empty(empty);
        }
    }

    private static readonly string ResourcesDir = Path.Combine(RepositoryRoot(), "src", "ImportaMI.App", "Resources");

    private static HashSet<string> Keys(string file) =>
        XDocument.Load(file).Root!.Elements("data").Select(d => d.Attribute("name")!.Value).ToHashSet();

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ImportaMI.slnx")))
                return dir.FullName;
        }

        throw new InvalidOperationException("Cartella del repository non trovata.");
    }
}
