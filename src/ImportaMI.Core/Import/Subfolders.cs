using ImportaMI.Core.Media;

namespace ImportaMI.Core.Import;

/// <summary>Come dividere i file all'interno della cartella di una sessione.</summary>
public enum SubfolderMode
{
    /// <summary>Tutti i file nella cartella della sessione.</summary>
    None,

    /// <summary>Foto in una sottocartella e video in un'altra.</summary>
    PhotosAndVideos,

    /// <summary>Una sottocartella per RAW, una per JPEG e una per i video.</summary>
    ByFormat,
}

/// <summary>Nomi delle sottocartelle, nella lingua scelta dall'utente.</summary>
public sealed record SubfolderNames(string Photos, string Raw, string Jpeg, string Videos)
{
    public static SubfolderNames English { get; } = new("Photos", "RAW", "JPG", "Videos");
}

public static class Subfolders
{
    /// <summary>Sottocartella in cui va il file, oppure <c>null</c> per la cartella della sessione.</summary>
    public static string? For(MediaKind kind, SubfolderMode mode, SubfolderNames names) => mode switch
    {
        SubfolderMode.None => null,
        SubfolderMode.PhotosAndVideos => kind == MediaKind.Video ? names.Videos : names.Photos,
        SubfolderMode.ByFormat => kind switch
        {
            MediaKind.Raw => names.Raw,
            MediaKind.Jpeg => names.Jpeg,
            MediaKind.Video => names.Videos,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
