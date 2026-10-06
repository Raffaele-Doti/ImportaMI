using Avalonia;
using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ImportaMI.App.Localization;
using ImportaMI.App.Services;
using ImportaMI.Core;
using ImportaMI.Core.Logging;

namespace ImportaMI.App;

sealed class Program
{
    private const string LanguageArgument = "--lang=";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // "--lang=en" o "--lang=it" forza la lingua; senza argomento si usa quella del sistema.
        var preferred = args
            .FirstOrDefault(a => a.StartsWith(LanguageArgument, StringComparison.OrdinalIgnoreCase))?
            [LanguageArgument.Length..];
        AppLanguage.Apply(preferred);

        // Log nella cartella dei dati dell'app, mai nella destinazione delle foto.
        AppLog.Start(new FileLog(AppDataFolder.Default.Logs));
        AppLog.Info($"Avvio ImportaMI {typeof(Program).Assembly.GetName().Version} su {RuntimeInformation.OSDescription}, " +
                    $"lingua {CultureInfo.CurrentUICulture.Name}, dati in {AppDataFolder.Default.Root}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error("Errore non gestito, l'app si chiude", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Errore in un'attività in background", e.Exception);
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppLog.Info("Chiusura ImportaMI");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
