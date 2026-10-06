using System.Globalization;
using System.Text;

namespace ImportaMI.Core.Import;

/// <summary>Struttura delle cartelle create nella destinazione.</summary>
public enum FolderLayout
{
    /// <summary><c>2026-09-27 Alba al lago</c></summary>
    DateAndName,

    /// <summary><c>2026/2026-09-27 Alba al lago</c></summary>
    YearThenDateAndName,

    /// <summary><c>2026/09/2026-09-27 Alba al lago</c></summary>
    YearMonthThenDateAndName,
}

public static class FolderNaming
{
    // Insieme dei caratteri non ammessi su Windows, il più restrittivo dei tre sistemi:
    // usarlo ovunque rende le cartelle portabili tra Windows, macOS e Linux.
    private static readonly char[] InvalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Rende un nome scelto dall'utente valido come nome di cartella: sostituisce con <c>_</c>
    /// i caratteri non ammessi e i caratteri di controllo, e toglie spazi e punti finali.
    /// </summary>
    public static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Trim())
            builder.Append(char.IsControl(c) || InvalidChars.Contains(c) ? '_' : c);

        return builder.ToString().TrimEnd('.', ' ');
    }

    /// <summary>
    /// Segmenti del percorso relativo alla cartella di destinazione, da unire con <see cref="Path.Combine(string[])"/>.
    /// Con un nome vuoto la cartella usa solo la data.
    /// </summary>
    public static IReadOnlyList<string> RelativeSegments(DateTime sessionStart, string name, FolderLayout layout)
    {
        var date = sessionStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var cleanName = Sanitize(name);
        var leaf = cleanName.Length == 0 ? date : $"{date} {cleanName}";
        var year = sessionStart.ToString("yyyy", CultureInfo.InvariantCulture);
        var month = sessionStart.ToString("MM", CultureInfo.InvariantCulture);

        return layout switch
        {
            FolderLayout.DateAndName => [leaf],
            FolderLayout.YearThenDateAndName => [year, leaf],
            FolderLayout.YearMonthThenDateAndName => [year, month, leaf],
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null),
        };
    }
}
