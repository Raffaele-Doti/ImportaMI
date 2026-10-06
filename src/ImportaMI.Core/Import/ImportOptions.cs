namespace ImportaMI.Core.Import;

/// <summary>Cosa fare quando nella destinazione esiste già un file con lo stesso nome.</summary>
public enum DuplicatePolicy
{
    /// <summary>Se il contenuto è identico (stesso hash) il file viene saltato; se è diverso il nuovo viene rinominato.</summary>
    SkipIdentical,

    /// <summary>Il nuovo file viene sempre rinominato, es. <c>IMG_4012 (1).CR3</c>.</summary>
    RenameAlways,

    /// <summary>Il file esistente viene sostituito, ma solo dopo che la nuova copia è completa e verificata.</summary>
    Overwrite,
}

/// <summary>Le scelte dell'utente nella schermata Destinazione.</summary>
public sealed record ImportOptions
{
    public required string DestinationRoot { get; init; }

    public FolderLayout Layout { get; init; } = FolderLayout.YearThenDateAndName;

    public ImportFormat Format { get; init; } = ImportFormat.Default;

    public SubfolderMode Subfolders { get; init; } = SubfolderMode.PhotosAndVideos;

    public SubfolderNames SubfolderNames { get; init; } = SubfolderNames.English;

    public DuplicatePolicy Duplicates { get; init; } = DuplicatePolicy.SkipIdentical;

    /// <summary>Rilegge ogni copia e ne confronta l'hash con quello dell'originale.</summary>
    public bool Verify { get; init; } = true;

    /// <summary>
    /// Non copia i file che esistono già, identici, in un'altra cartella dell'archivio
    /// (vedi <see cref="PlannedFile.ExistingCopy"/>).
    /// </summary>
    public bool SkipArchiveDuplicates { get; init; } = true;
}
