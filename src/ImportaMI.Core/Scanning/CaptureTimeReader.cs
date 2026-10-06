using ImportaMI.Core.Media;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace ImportaMI.Core.Scanning;

/// <summary>Esito della lettura dell'orario di scatto di un file.</summary>
/// <param name="Time">Orario trovato nei metadati, oppure <c>null</c>.</param>
/// <param name="Error">Motivo per cui i metadati non si sono potuti leggere, se è il caso.</param>
public readonly record struct CaptureTimeResult(DateTime? Time, string? Error = null)
{
    public static CaptureTimeResult NotFound { get; } = new(null);
}

public interface ICaptureTimeReader
{
    /// <summary>
    /// Legge l'orario di scatto dai metadati. Non solleva eccezioni per file corrotti o formati
    /// non riconosciuti (li riporta in <see cref="CaptureTimeResult.Error"/>); solleva
    /// <see cref="IOException"/> o <see cref="UnauthorizedAccessException"/> se il file non si può aprire.
    /// </summary>
    CaptureTimeResult Read(string path, MediaKind kind);
}

/// <summary>Legge l'orario di scatto con MetadataExtractor.</summary>
public sealed class MetadataCaptureTimeReader : ICaptureTimeReader
{
    // Le date di QuickTime partono dal 1904: un valore così vecchio significa "non impostato".
    private static readonly DateTime OldestPlausible = new(1980, 1, 1);

    public CaptureTimeResult Read(string path, MediaKind kind)
    {
        IReadOnlyList<MetadataExtractor.Directory> directories;
        try
        {
            directories = ImageMetadataReader.ReadMetadata(path);
        }
        catch (Exception e) when (e is not IOException and not UnauthorizedAccessException)
        {
            // File troncati o corrotti possono far fallire il parser in modi diversi
            // (ImageProcessingException, ma anche errori di indice o formato): non sono un motivo
            // per fermare la scansione.
            return new CaptureTimeResult(null, e.Message);
        }

        var time = kind == MediaKind.Video ? FromVideo(directories) : FromPhoto(directories);
        return time is { } t && t >= OldestPlausible
            ? new CaptureTimeResult(DateTime.SpecifyKind(t, DateTimeKind.Unspecified))
            : CaptureTimeResult.NotFound;
    }

    private static DateTime? FromPhoto(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        // In ordine di preferenza: momento dello scatto, momento della digitalizzazione, ultima modifica in camera.
        foreach (var directory in directories.OfType<ExifSubIfdDirectory>())
        {
            if (directory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original))
                return original;
            if (directory.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitized))
                return digitized;
        }

        foreach (var directory in directories.OfType<ExifIfd0Directory>())
        {
            if (directory.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var modified))
                return modified;
        }

        return null;
    }

    private static DateTime? FromVideo(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        // L'intestazione di MP4 e MOV registra la creazione in UTC: la riportiamo all'ora locale
        // del computer, che di solito coincide con quella impostata sulla fotocamera.
        foreach (var directory in directories.OfType<QuickTimeMovieHeaderDirectory>())
        {
            if (directory.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var created))
                return DateTime.SpecifyKind(created, DateTimeKind.Utc).ToLocalTime();
        }

        return null;
    }
}
