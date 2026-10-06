using Avalonia;
using Avalonia.Headless;
using ImportaMI.App.Services;

[assembly: AvaloniaTestApplication(typeof(ImportaMI.App.Tests.TestAppBuilder))]

namespace ImportaMI.App.Tests;

/// <summary>L'app vera, senza finestra a schermo ma con il rendering Skia, così si possono salvare screenshot.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>Al posto del selettore di cartelle del sistema restituisce sempre la stessa cartella.</summary>
internal sealed class FixedFolderPicker(string folder) : IFolderPicker
{
    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(folder);
}
