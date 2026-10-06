using ImportaMI.Core.Import;
using ImportaMI.Core.Settings;

namespace ImportaMI.Core.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Saved_settings_are_read_back()
    {
        using var folder = new TempSource();
        var store = new SettingsStore(Path.Combine(folder.Root, "sub", "settings.json"));
        var settings = new ImportSettings
        {
            DestinationRoot = @"D:\Foto",
            Photos = PhotoFormats.RawOnly,
            Subfolders = SubfolderMode.ByFormat,
            SkipArchiveDuplicates = false,
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Settings_from_the_old_location_are_moved_once()
    {
        using var folder = new TempSource();
        var oldStore = new SettingsStore(Path.Combine(folder.Root, "Roaming", "ImportaMI", "settings.json"));
        oldStore.Save(new ImportSettings { DestinationRoot = @"D:\Foto" });
        var store = new SettingsStore(Path.Combine(folder.Root, "Local", "ImportaMI", "settings.json"));

        store.MigrateFrom(oldStore.FilePath);

        Assert.Equal(@"D:\Foto", store.Load().DestinationRoot);
        Assert.False(Directory.Exists(Path.Combine(folder.Root, "Roaming", "ImportaMI")));
    }

    [Fact]
    public void Migration_never_overwrites_newer_settings()
    {
        using var folder = new TempSource();
        var oldStore = new SettingsStore(Path.Combine(folder.Root, "old", "settings.json"));
        oldStore.Save(new ImportSettings { DestinationRoot = @"D:\Vecchia" });
        var store = new SettingsStore(Path.Combine(folder.Root, "new", "settings.json"));
        store.Save(new ImportSettings { DestinationRoot = @"D:\Nuova" });

        store.MigrateFrom(oldStore.FilePath);

        Assert.Equal(@"D:\Nuova", store.Load().DestinationRoot);
    }

    [Fact]
    public void Missing_file_gives_the_defaults()
    {
        using var folder = new TempSource();

        Assert.Equal(new ImportSettings(), new SettingsStore(Path.Combine(folder.Root, "settings.json")).Load());
    }

    [Fact]
    public void Damaged_file_gives_the_defaults()
    {
        using var folder = new TempSource();
        var path = folder.WriteText("settings.json", "{ questo non è json");

        Assert.Equal(new ImportSettings(), new SettingsStore(path).Load());
    }
}
