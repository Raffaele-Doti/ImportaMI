using System.Buffers.Binary;
using System.Text;

namespace ImportaMI.Core.Tests;

/// <summary>Cartella temporanea che simula una scheda o una chiavetta; si cancella da sola.</summary>
internal sealed class TempSource : IDisposable
{
    public TempSource()
    {
        Root = Path.Combine(Path.GetTempPath(), "ImportaMI-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Write(string relativePath, byte[] content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    public string WriteText(string relativePath, string text) => Write(relativePath, Encoding.UTF8.GetBytes(text));

    public string[] Files(string pattern) => Directory.GetFiles(Root, pattern, SearchOption.AllDirectories);

    public void Dispose()
    {
        if (!Directory.Exists(Root))
            return;
        try
        {
            // I file in sola lettura (protetti in fotocamera) bloccherebbero la cancellazione su Windows.
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException) { }
    }
}

internal static class TestJpeg
{
    /// <summary>
    /// Il JPEG più piccolo che contiene un orario EXIF: un segmento APP1 con IFD0,
    /// un puntatore alla Exif SubIFD e il tag DateTimeOriginal ("yyyy:MM:dd HH:mm:ss").
    /// </summary>
    public static byte[] WithDateTimeOriginal(string exifDate)
    {
        var tiff = new List<byte>();
        void U16(int v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, (ushort)v); tiff.AddRange(b); }
        void U32(int v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, (uint)v); tiff.AddRange(b); }
        void Entry(int tag, int type, int count, int value) { U16(tag); U16(type); U32(count); U32(value); }

        tiff.AddRange("II"u8.ToArray());
        U16(42);
        U32(8);                           // IFD0 subito dopo l'intestazione
        U16(1);
        Entry(0x8769, 4, 1, 26);          // puntatore alla Exif SubIFD (offset 26)
        U32(0);
        U16(1);
        Entry(0x9003, 2, 20, 44);         // DateTimeOriginal, 20 caratteri ASCII all'offset 44
        U32(0);
        tiff.AddRange(Encoding.ASCII.GetBytes(exifDate + "\0"));

        var app1 = Encoding.ASCII.GetBytes("Exif\0\0").Concat(tiff).ToArray();
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(app1.Length + 2));

        return [0xFF, 0xD8, 0xFF, 0xE1, .. length, .. app1, 0xFF, 0xD9];
    }
}
