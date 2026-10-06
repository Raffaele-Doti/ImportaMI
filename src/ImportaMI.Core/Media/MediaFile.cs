namespace ImportaMI.Core.Media;

/// <summary>Da dove viene l'orario di scatto di un file.</summary>
public enum CaptureTimeSource
{
    /// <summary>Metadati scritti dalla fotocamera (EXIF per le foto, intestazione del filmato per i video).</summary>
    Metadata,

    /// <summary>Data di modifica del file: usata solo quando i metadati mancano o sono illeggibili.</summary>
    FileDate,
}

/// <summary>Un file letto dalla sorgente, con l'orario di scatto.</summary>
/// <param name="SourcePath">Percorso completo sulla scheda o sul disco sorgente.</param>
/// <param name="Kind">Categoria del file.</param>
/// <param name="CaptureTime">
/// Orario di scatto (EXIF DateTimeOriginal). È l'ora locale impostata sulla fotocamera,
/// senza fuso orario, quindi resta <see cref="DateTimeKind.Unspecified"/>.
/// </param>
/// <param name="SizeBytes">Dimensione del file.</param>
/// <param name="TimeSource">Da dove viene <paramref name="CaptureTime"/>.</param>
public sealed record MediaFile(
    string SourcePath,
    MediaKind Kind,
    DateTime CaptureTime,
    long SizeBytes,
    CaptureTimeSource TimeSource = CaptureTimeSource.Metadata)
{
    public string FileName => Path.GetFileName(SourcePath);

    /// <summary>Nome senza estensione: è ciò che accomuna RAW e JPEG dello stesso scatto.</summary>
    public string Stem => Path.GetFileNameWithoutExtension(SourcePath);

    public string Directory => Path.GetDirectoryName(SourcePath) ?? string.Empty;
}
