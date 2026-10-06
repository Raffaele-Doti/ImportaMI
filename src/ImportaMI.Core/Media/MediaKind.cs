namespace ImportaMI.Core.Media;

/// <summary>Categoria di un file multimediale trovato sulla scheda.</summary>
public enum MediaKind
{
    /// <summary>File RAW della fotocamera (CR3, NEF, ARW, RAF, DNG…).</summary>
    Raw,

    /// <summary>Immagine già sviluppata: JPEG, HEIF/HEIC, PNG, WebP o TIFF.</summary>
    Jpeg,

    /// <summary>Filmato (MP4, MOV…).</summary>
    Video,
}

public static class MediaKinds
{
    private static readonly Dictionary<string, MediaKind> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cr2"] = MediaKind.Raw, [".cr3"] = MediaKind.Raw, [".crw"] = MediaKind.Raw,
        [".nef"] = MediaKind.Raw, [".nrw"] = MediaKind.Raw,
        [".arw"] = MediaKind.Raw, [".srf"] = MediaKind.Raw, [".sr2"] = MediaKind.Raw,
        [".raf"] = MediaKind.Raw, [".orf"] = MediaKind.Raw, [".rw2"] = MediaKind.Raw,
        [".pef"] = MediaKind.Raw, [".srw"] = MediaKind.Raw, [".dng"] = MediaKind.Raw,
        [".3fr"] = MediaKind.Raw, [".iiq"] = MediaKind.Raw, [".rwl"] = MediaKind.Raw,

        [".jpg"] = MediaKind.Jpeg, [".jpeg"] = MediaKind.Jpeg,
        [".heic"] = MediaKind.Jpeg, [".heif"] = MediaKind.Jpeg, [".hif"] = MediaKind.Jpeg,
        [".png"] = MediaKind.Jpeg, [".webp"] = MediaKind.Jpeg, [".tif"] = MediaKind.Jpeg, [".tiff"] = MediaKind.Jpeg,

        [".mp4"] = MediaKind.Video, [".mov"] = MediaKind.Video, [".mts"] = MediaKind.Video,
        [".m2ts"] = MediaKind.Video, [".avi"] = MediaKind.Video,
    };

    /// <summary>Restituisce la categoria del file, o <c>null</c> se l'estensione non è supportata.</summary>
    public static MediaKind? FromPath(string path) =>
        ByExtension.TryGetValue(Path.GetExtension(path), out var kind) ? kind : null;
}
