using System.Globalization;
using ImportaMI.Core.Media;

namespace ImportaMI.Core.Tests;

internal static class TestData
{
    public static readonly string Dcim = Path.Combine("E:", "DCIM", "100EOSR6");

    /// <summary>Crea un file nella cartella DCIM di esempio, con orario "yyyy-MM-dd HH:mm:ss".</summary>
    public static MediaFile FileAt(string name, string time, long size = 25_000_000, string? folder = null)
    {
        var path = Path.Combine(folder ?? Dcim, name);
        var kind = MediaKinds.FromPath(path) ?? throw new ArgumentException($"Estensione non supportata: {name}");
        return new MediaFile(path, kind, At(time), size);
    }

    public static DateTime At(string time) =>
        DateTime.ParseExact(time, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Uno scatto con un solo RAW, nominato in base all'orario per comodità.</summary>
    public static Shot RawShot(string time) => new([FileAt($"IMG_{At(time):HHmmss}.CR3", time)]);
}
