namespace ImportaMI.Core.Localization;

/// <summary>Sceglie la lingua dell'interfaccia tra quelle disponibili.</summary>
public static class LanguageSelection
{
    /// <summary>Lingua usata quando né la scelta dell'utente né quella del sistema sono disponibili.</summary>
    public const string Fallback = "en";

    public static IReadOnlyList<string> Supported { get; } = ["en", "it"];

    /// <summary>
    /// Restituisce il codice a due lettere della lingua da usare.
    /// Vince la scelta esplicita dell'utente, poi la lingua del sistema operativo, infine l'inglese.
    /// </summary>
    /// <param name="preferred">Scelta esplicita, es. <c>it</c> o <c>en-GB</c>; può essere vuota.</param>
    /// <param name="systemCulture">Nome della cultura dell'interfaccia del sistema, es. <c>it-IT</c>.</param>
    public static string Resolve(string? preferred, string? systemCulture)
    {
        foreach (var candidate in new[] { preferred, systemCulture })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var language = candidate.Trim().Split('-', '_')[0].ToLowerInvariant();
            if (Supported.Contains(language))
                return language;
        }

        return Fallback;
    }
}
