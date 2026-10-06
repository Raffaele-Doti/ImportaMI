namespace ImportaMI.Core;

/// <summary>
/// La cartella dei dati di ImportaMI: impostazioni, report e log. Non sta mai nella destinazione
/// scelta dall'utente, dove finiscono solo le foto.
/// </summary>
/// <remarks>
/// Si usa la cartella dei dati locali dell'utente, che esiste ed è scrivibile su tutti e tre i sistemi:
/// <c>%LOCALAPPDATA%\ImportaMI</c> su Windows, <c>~/Library/Application Support/ImportaMI</c> su macOS,
/// <c>~/.local/share/ImportaMI</c> su Linux. ProgramData è condivisa tra gli utenti ed esiste solo su Windows.
/// </remarks>
public sealed class AppDataFolder(string root)
{
    public static AppDataFolder Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "ImportaMI"));

    public string Root { get; } = root;

    public string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>Un CSV per ogni importazione, con l'esito di ogni file.</summary>
    public string Reports => Path.Combine(Root, "reports");

    /// <summary>Un file di log al giorno.</summary>
    public string Logs => Path.Combine(Root, "logs");
}
