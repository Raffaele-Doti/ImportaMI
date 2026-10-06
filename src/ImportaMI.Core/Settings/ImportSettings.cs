using System.Text.Json;
using System.Text.Json.Serialization;
using ImportaMI.Core.Import;

namespace ImportaMI.Core.Settings;

/// <summary>Scelte dell'utente ricordate tra un'importazione e l'altra.</summary>
public sealed record ImportSettings
{
    /// <summary>Cartella di destinazione; vuota al primo avvio (l'app propone la cartella Immagini).</summary>
    public string DestinationRoot { get; init; } = string.Empty;

    public FolderLayout Layout { get; init; } = FolderLayout.YearThenDateAndName;

    public PhotoFormats Photos { get; init; } = PhotoFormats.RawAndJpeg;

    public bool IncludeVideos { get; init; } = true;

    public SubfolderMode Subfolders { get; init; } = SubfolderMode.PhotosAndVideos;

    public DuplicatePolicy Duplicates { get; init; } = DuplicatePolicy.SkipIdentical;

    public bool Verify { get; init; } = true;

    public bool EjectWhenDone { get; init; } = true;

    public bool ProposeCleanup { get; init; } = true;

    /// <summary>Salta i file che si trovano già, identici, in qualsiasi cartella dell'archivio.</summary>
    public bool SkipArchiveDuplicates { get; init; } = true;
}

/// <summary>Legge e salva le impostazioni in un file JSON.</summary>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Percorso predefinito, nella cartella dei dati dell'app.</summary>
    public static string DefaultPath => AppDataFolder.Default.SettingsFile;

    public string FilePath { get; } = path;

    /// <summary>Un file mancante o rovinato non è un errore: si riparte dalle impostazioni predefinite.</summary>
    public ImportSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<ImportSettings>(File.ReadAllText(FilePath), Json) ?? new ImportSettings()
                : new ImportSettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ImportSettings();
        }
    }

    /// <summary>
    /// Le prime versioni salvavano le impostazioni in <c>ApplicationData</c> (Roaming su Windows).
    /// Se il file nuovo non c'è ancora, sposta quello vecchio, così l'utente non perde le scelte fatte.
    /// </summary>
    public void MigrateFrom(string oldPath)
    {
        try
        {
            if (File.Exists(FilePath) || !File.Exists(oldPath))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.Move(oldPath, FilePath);
            var oldFolder = Path.GetDirectoryName(oldPath);
            if (oldFolder is not null && !Directory.EnumerateFileSystemEntries(oldFolder).Any())
                Directory.Delete(oldFolder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Percorso delle impostazioni nelle prime versioni.</summary>
    public static string LegacyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ImportaMI", "settings.json");

    /// <summary>Salva senza mai far fallire l'app: perdere le preferenze non giustifica un errore.</summary>
    public void Save(ImportSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Json));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
