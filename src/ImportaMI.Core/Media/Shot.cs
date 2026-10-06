namespace ImportaMI.Core.Media;

/// <summary>
/// Uno scatto: i file nati dallo stesso clic (es. IMG_4012.CR3 + IMG_4012.JPG) oppure un singolo video.
/// </summary>
public sealed class Shot
{
    public Shot(IEnumerable<MediaFile> files)
    {
        Files = files.OrderBy(f => f.Kind).ThenBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase).ToArray();
        if (Files.Count == 0)
            throw new ArgumentException("Uno scatto deve contenere almeno un file.", nameof(files));

        // Se un file dello scatto ha l'orario dai metadati (es. il JPEG) vale più della data
        // di un altro file (es. un RAW che la libreria non sa leggere).
        var reliable = Files.Where(f => f.TimeSource == CaptureTimeSource.Metadata).ToArray();
        CaptureTime = (reliable.Length > 0 ? reliable : Files).Min(f => f.CaptureTime);
        TimeSource = reliable.Length > 0 ? CaptureTimeSource.Metadata : CaptureTimeSource.FileDate;
    }

    public CaptureTimeSource TimeSource { get; }

    public IReadOnlyList<MediaFile> Files { get; }

    public DateTime CaptureTime { get; }

    public bool IsVideo => Files.All(f => f.Kind == MediaKind.Video);

    public bool HasRaw => Files.Any(f => f.Kind == MediaKind.Raw);

    public bool HasJpeg => Files.Any(f => f.Kind == MediaKind.Jpeg);

    public long SizeBytes => Files.Sum(f => f.SizeBytes);
}

public static class ShotBuilder
{
    /// <summary>
    /// Raggruppa i file in scatti: stessa cartella e stesso nome (senza estensione) formano uno scatto.
    /// I video non vengono mai abbinati alle foto.
    /// </summary>
    public static IReadOnlyList<Shot> Build(IEnumerable<MediaFile> files) =>
        files
            .GroupBy(f => (
                Directory: f.Directory.ToUpperInvariant(),
                Stem: f.Stem.ToUpperInvariant(),
                IsVideo: f.Kind == MediaKind.Video))
            .Select(g => new Shot(g))
            .OrderBy(s => s.CaptureTime)
            .ThenBy(s => s.Files[0].SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
