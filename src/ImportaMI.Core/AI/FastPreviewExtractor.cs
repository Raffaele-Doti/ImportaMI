using System;
using System.IO;
using System.Linq;
using ImportaMI.Core.Media;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ImportaMI.Core.AI;

/// <summary>
/// Estrae rapidamente i byte dell'immagine o della miniatura incorporata senza decodificare l'intero file RAW.
/// Funziona istantaneamente sia per JPEG che per i file RAW delle fotocamere.
/// </summary>
public static class FastPreviewExtractor
{
    private static readonly string[] DecodableExts = [".JPG", ".JPEG", ".PNG", ".WEBP"];

    public static byte[]? ExtractBytes(Shot shot)
    {
        // 1. Cerca prima un file JPEG compagno nello stesso scatto (RAW + JPEG)
        var jpegFile = shot.Files.FirstOrDefault(f => f.Kind == MediaKind.Jpeg && IsDecodable(f.SourcePath));
        if (jpegFile is not null && File.Exists(jpegFile.SourcePath))
        {
            try
            {
                return File.ReadAllBytes(jpegFile.SourcePath);
            }
            catch
            {
                // Fallback su RAW se il file non è leggibile
            }
        }

        // 2. Se è solo RAW, estrai l'anteprima JPEG incorporata nell'intestazione EXIF
        var rawFile = shot.Files.FirstOrDefault(f => f.Kind == MediaKind.Raw);
        if (rawFile is not null && File.Exists(rawFile.SourcePath))
        {
            return ExtractRawPreview(rawFile.SourcePath);
        }

        return null;
    }

    public static byte[]? ExtractRawPreview(string path)
    {
        try
        {
            var metadata = ImageMetadataReader.ReadMetadata(path);
            var thumbnail = metadata.OfType<ExifThumbnailDirectory>().FirstOrDefault();
            if (thumbnail is null
                || !thumbnail.TryGetInt64(ExifThumbnailDirectory.TagThumbnailOffset, out var offset)
                || !thumbnail.TryGetInt32(ExifThumbnailDirectory.TagThumbnailLength, out var length)
                || length <= 0)
                return null;

            using var stream = File.OpenRead(path);
            if (offset + length > stream.Length)
                return null;

            var bytes = new byte[length];
            stream.Seek(offset, SeekOrigin.Begin);
            stream.ReadExactly(bytes);

            // Verifica che inizi con i magic bytes JPEG FF D8
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                return bytes;
            }
        }
        catch
        {
            // Errori di lettura o RAW senza miniatura standard
        }

        return null;
    }

    private static bool IsDecodable(string path)
    {
        var ext = Path.GetExtension(path);
        return DecodableExts.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }
}
