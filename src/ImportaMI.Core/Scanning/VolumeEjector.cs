using System.Diagnostics;

namespace ImportaMI.Core.Scanning;

/// <summary>Espelle in sicurezza una scheda o un disco, con i comandi del sistema operativo.</summary>
public static class VolumeEjector
{
    /// <summary>
    /// Restituisce <c>true</c> se il volume risulta espulso. Non solleva eccezioni:
    /// se l'espulsione non riesce (file aperti, permessi) l'utente può farlo dal sistema.
    /// </summary>
    public static async Task<bool> TryEjectAsync(string root, CancellationToken cancellationToken = default)
    {
        try
        {
            var start = Command(root);
            if (start is null)
                return false;

            using var process = Process.Start(start);
            if (process is null)
                return false;
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

            // Il comando di Windows non restituisce un esito: controlliamo che il volume sia sparito.
            for (var i = 0; i < 10 && Directory.Exists(root); i++)
                await Task.Delay(300, cancellationToken);
            return !Directory.Exists(root);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException or IOException)
        {
            return false;
        }
    }

    private static ProcessStartInfo? Command(string root)
    {
        if (OperatingSystem.IsWindows())
        {
            // "Espelli" di Esplora risorse, lo stesso del menu contestuale dell'unità.
            var drive = root.TrimEnd('\\');
            var script = $"(New-Object -ComObject Shell.Application).Namespace(17).ParseName('{drive.Replace("'", "''")}').InvokeVerb('Eject')";
            return Hidden("powershell.exe", "-NoProfile", "-NonInteractive", "-Command", script);
        }

        if (OperatingSystem.IsMacOS())
            return Hidden("diskutil", "eject", root);

        if (OperatingSystem.IsLinux())
            return Hidden("gio", "mount", "--eject", root);

        return null;
    }

    private static ProcessStartInfo Hidden(string file, params string[] arguments)
    {
        var info = new ProcessStartInfo(file) { CreateNoWindow = true, UseShellExecute = false };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        return info;
    }
}
