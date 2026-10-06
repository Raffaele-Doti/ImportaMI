namespace ImportaMI.Core.Scanning;

/// <summary>Una memoria da cui importare: scheda, chiavetta, disco esterno.</summary>
public sealed record StorageSource(string RootPath, string Label, bool IsRemovable, long TotalBytes, long FreeBytes);

/// <summary>Elenca le memorie collegate su Windows, macOS e Linux.</summary>
public static class StorageSources
{
    public static IReadOnlyList<StorageSource> Detect()
    {
        var sources = new List<StorageSource>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || !IsCandidate(drive))
                    continue;

                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? Path.GetFileName(drive.Name.TrimEnd('\\', '/')) : drive.VolumeLabel;
                sources.Add(new StorageSource(
                    drive.RootDirectory.FullName,
                    string.IsNullOrWhiteSpace(label) ? drive.Name : label,
                    drive.DriveType == DriveType.Removable,
                    drive.TotalSize,
                    drive.AvailableFreeSpace));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Unità che sparisce mentre la leggiamo, o lettore vuoto: la ignoriamo.
            }
        }

        // Prima le memorie rimovibili, che sono quasi sempre la sorgente cercata.
        return sources.OrderByDescending(s => s.IsRemovable).ThenBy(s => s.RootPath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Spazio totale e libero del volume che contiene <paramref name="path"/> (anche se la cartella non esiste ancora),
    /// oppure <c>null</c> se il volume non è disponibile. Su macOS e Linux sceglie il punto di montaggio più specifico.
    /// </summary>
    public static (string Root, long Total, long Free)? SpaceFor(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var drive = DriveInfo.GetDrives()
                .Where(d => full.StartsWith(d.RootDirectory.FullName, comparison))
                .OrderByDescending(d => d.RootDirectory.FullName.Length)
                .FirstOrDefault(d => d.IsReady);
            return drive is null ? null : (drive.RootDirectory.FullName, drive.TotalSize, drive.AvailableFreeSpace);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsCandidate(DriveInfo drive)
    {
        // Le unità di rete scollegate fanno attendere a lungo e non sono schede o dischi da importare.
        if (drive.DriveType is DriveType.CDRom or DriveType.Ram or DriveType.Network or DriveType.NoRootDirectory or DriveType.Unknown)
            return false;

        var root = drive.RootDirectory.FullName;

        if (OperatingSystem.IsWindows())
        {
            // Il disco di sistema non è mai una sorgente di foto da importare.
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
            return !string.Equals(root, systemRoot, StringComparison.OrdinalIgnoreCase);
        }

        // Su macOS e Linux DriveInfo elenca anche i file system di sistema: teniamo solo i punti
        // di montaggio dei dischi esterni.
        if (OperatingSystem.IsMacOS())
            return root.StartsWith("/Volumes/", StringComparison.Ordinal);

        return root.StartsWith("/media/", StringComparison.Ordinal)
            || root.StartsWith("/run/media/", StringComparison.Ordinal)
            || root.StartsWith("/mnt/", StringComparison.Ordinal);
    }
}
