using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ImportaMI.Core.Media;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ImportaMI.App.Services;

/// <summary>Miniatura decodificata e rotazione da applicare (orientamento EXIF).</summary>
public sealed record Thumbnail(Bitmap Image, double Rotation);

/// <summary>
/// Crea le miniature degli scatti: dal JPEG quando c'è, altrimenti dall'anteprima incorporata
/// nei RAW basati su TIFF (NEF, ARW, CR2, DNG…). Video e RAW senza anteprima restano senza miniatura.
/// </summary>
public static class Thumbnails
{
    private const int Width = 240;

    // Poche decodifiche in parallelo: la scheda è lenta e la UI deve restare reattiva.
    private static readonly SemaphoreSlim Slots = new(3);
    private static readonly ConcurrentDictionary<string, Task<Thumbnail?>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Task<Thumbnail?> ForShotAsync(Shot shot)
    {
        var file = shot.Files.FirstOrDefault(f => f.Kind == MediaKind.Jpeg && IsDecodable(f.SourcePath))
                   ?? shot.Files.FirstOrDefault(f => f.Kind == MediaKind.Raw);
        return file is null ? Task.FromResult<Thumbnail?>(null) : Cache.GetOrAdd(file.SourcePath, _ => LoadAsync(file));
    }

    private static async Task<Thumbnail?> LoadAsync(MediaFile file)
    {
        await Slots.WaitAsync();
        try
        {
            return await Task.Run(() => file.Kind == MediaKind.Jpeg ? FromJpeg(file.SourcePath) : FromRawPreview(file.SourcePath));
        }
        catch (Exception)
        {
            // Una miniatura che non si riesce a creare non deve mai disturbare: resta il segnaposto.
            return null;
        }
        finally
        {
            Slots.Release();
        }
    }

    private static Thumbnail? FromJpeg(string path)
    {
        var rotation = Rotation(ImageMetadataReader.ReadMetadata(path));
        using var stream = File.OpenRead(path);
        return new Thumbnail(Bitmap.DecodeToWidth(stream, Width, BitmapInterpolationMode.MediumQuality), rotation);
    }

    private static Thumbnail? FromRawPreview(string path)
    {
        var metadata = ImageMetadataReader.ReadMetadata(path);
        var thumbnail = metadata.OfType<ExifThumbnailDirectory>().FirstOrDefault();
        if (thumbnail is null
            || !thumbnail.TryGetInt64(ExifThumbnailDirectory.TagThumbnailOffset, out var offset)
            || !thumbnail.TryGetInt32(ExifThumbnailDirectory.TagThumbnailLength, out var length)
            || length <= 0)
            return null;

        // Nei RAW basati su TIFF l'offset è relativo all'inizio del file.
        using var stream = File.OpenRead(path);
        if (offset + length > stream.Length)
            return null;
        var bytes = new byte[length];
        stream.Seek(offset, SeekOrigin.Begin);
        stream.ReadExactly(bytes);
        if (bytes.Length < 2 || bytes[0] != 0xFF || bytes[1] != 0xD8)
            return null;

        using var jpeg = new MemoryStream(bytes);
        return new Thumbnail(Bitmap.DecodeToWidth(jpeg, Width, BitmapInterpolationMode.MediumQuality), Rotation(metadata));
    }

    private static double Rotation(System.Collections.Generic.IReadOnlyList<MetadataExtractor.Directory> metadata)
    {
        var ifd0 = metadata.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (ifd0 is null || !ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var orientation))
            return 0;
        return orientation switch
        {
            3 => 180,
            6 => 90,
            8 => 270,
            _ => 0,
        };
    }

    // HEIC/HEIF e TIFF non si decodificano con Skia: per quei file si prova l'anteprima del RAW o il segnaposto.
    private static bool IsDecodable(string path) =>
        Path.GetExtension(path).ToUpperInvariant() is ".JPG" or ".JPEG" or ".PNG" or ".WEBP";
}
