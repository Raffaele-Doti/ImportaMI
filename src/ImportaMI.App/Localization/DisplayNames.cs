using System;
using System.Globalization;
using ImportaMI.App.Resources;
using ImportaMI.Core.Import;
using ImportaMI.Core.Scanning;
using ImportaMI.Core.Sessions;

namespace ImportaMI.App.Localization;

/// <summary>Testi tradotti per i valori del modello mostrati nell'interfaccia.</summary>
public static class DisplayNames
{
    public static string For(PhotoFormats formats) => formats switch
    {
        PhotoFormats.RawAndJpeg => Strings.FormatRawAndJpeg,
        PhotoFormats.RawOnly => Strings.FormatRawOnly,
        PhotoFormats.JpegOnly => Strings.FormatJpegOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(formats), formats, null),
    };

    public static string For(SubfolderMode mode) => mode switch
    {
        SubfolderMode.None => Strings.SubfolderModeNone,
        SubfolderMode.PhotosAndVideos => Strings.SubfolderModePhotosAndVideos,
        SubfolderMode.ByFormat => Strings.SubfolderModeByFormat,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static string For(ScanIssueKind kind) => kind switch
    {
        ScanIssueKind.FolderNotAccessible => Strings.IssueFolderNotAccessible,
        ScanIssueKind.FileNotReadable => Strings.IssueFileNotReadable,
        ScanIssueKind.EmptyFile => Strings.IssueEmptyFile,
        ScanIssueKind.NoCaptureTime => Strings.IssueNoCaptureTime,
        ScanIssueKind.MetadataUnreadable => Strings.IssueMetadataUnreadable,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Nomi delle sottocartelle nella lingua dell'interfaccia.</summary>
    public static SubfolderNames Subfolders => new(Strings.SubfolderPhotos, "RAW", "JPG", Strings.SubfolderVideos);
}

/// <summary>Numeri, dimensioni e durate formattati nella lingua dell'interfaccia.</summary>
public static class Format
{
    private static CultureInfo Culture => CultureInfo.CurrentUICulture;

    public static string Text(string template, params object[] args) => string.Format(Culture, template, args);

    public static string Number(int value) => value.ToString("N0", Culture);

    public static string Size(long bytes)
    {
        const double Kb = 1024, Mb = Kb * 1024, Gb = Mb * 1024, Tb = Gb * 1024;
        return bytes switch
        {
            >= (long)Tb => (bytes / Tb).ToString("0.00", Culture) + " TB",
            >= (long)Gb => (bytes / Gb).ToString("0.0", Culture) + " GB",
            >= (long)Mb => (bytes / Mb).ToString("0", Culture) + " MB",
            _ => (bytes / Kb).ToString("0", Culture) + " KB",
        };
    }

    public static string Duration(TimeSpan span)
    {
        if (span.TotalSeconds < 59.5)
            return Text(Strings.DurationSeconds, Math.Max(0, (int)Math.Round(span.TotalSeconds)));
        var minutes = (int)Math.Round(span.TotalMinutes);
        var hours = minutes / 60;
        if (hours == 0)
            return Text(Strings.DurationMinutes, minutes);
        return minutes % 60 == 0 ? Text(Strings.DurationHours, hours) : Text(Strings.DurationHoursMinutes, hours, minutes % 60);
    }

    public static string Date(DateTime date) => date.ToString("ddd d MMM yyyy", Culture);

    public static string Time(DateTime time) => time.ToString("HH:mm", Culture);
}
