using System.Globalization;
using ImportaMI.App.Resources;
using ImportaMI.Core.Localization;

namespace ImportaMI.App.Localization;

public static class AppLanguage
{
    /// <summary>
    /// Imposta la lingua dell'interfaccia. Va chiamato prima di creare le finestre:
    /// i testi vengono letti al caricamento delle viste.
    /// </summary>
    /// <param name="preferred">Lingua scelta dall'utente (es. argomento <c>--lang=en</c>), oppure <c>null</c>.</param>
    public static void Apply(string? preferred)
    {
        var language = LanguageSelection.Resolve(preferred, CultureInfo.CurrentUICulture.Name);
        var culture = CultureInfo.GetCultureInfo(language);

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Strings.Culture = culture;
    }
}
