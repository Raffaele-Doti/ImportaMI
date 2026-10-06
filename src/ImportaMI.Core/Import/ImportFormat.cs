using ImportaMI.Core.Media;

namespace ImportaMI.Core.Import;

/// <summary>Quali file delle foto importare.</summary>
public enum PhotoFormats
{
    RawAndJpeg,
    RawOnly,
    JpegOnly,
}

/// <summary>Scelta dell'utente sui formati da copiare. I file esclusi restano sulla scheda.</summary>
public sealed record ImportFormat(PhotoFormats Photos, bool IncludeVideos)
{
    public static ImportFormat Default { get; } = new(PhotoFormats.RawAndJpeg, IncludeVideos: true);

    public bool Includes(MediaFile file) => file.Kind switch
    {
        MediaKind.Raw => Photos != PhotoFormats.JpegOnly,
        MediaKind.Jpeg => Photos != PhotoFormats.RawOnly,
        MediaKind.Video => IncludeVideos,
        _ => false,
    };

    /// <summary>
    /// File dello scatto da copiare. Può essere vuoto: per esempio uno scatto solo RAW
    /// quando l'utente ha scelto "Solo JPEG".
    /// </summary>
    public IEnumerable<MediaFile> FilesToImport(Shot shot) => shot.Files.Where(Includes);
}
