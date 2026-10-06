using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ImportaMI.App.Services;

public interface IFolderPicker
{
    /// <summary>Apre il selettore di cartelle del sistema; restituisce <c>null</c> se l'utente annulla.</summary>
    Task<string?> PickFolderAsync(string title);
}

/// <summary>Selettore di cartelle nativo di Windows, macOS e Linux.</summary>
public sealed class FolderPicker(Func<TopLevel?> topLevel) : IFolderPicker
{
    public async Task<string?> PickFolderAsync(string title)
    {
        if (topLevel() is not { } owner)
            return null;

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}

/// <summary>Apre file e cartelle con l'applicazione predefinita del sistema.</summary>
public static class Shell
{
    public static void Open(string path)
    {
        try
        {
            var info = OperatingSystem.IsWindows() ? new ProcessStartInfo("explorer.exe", $"\"{path}\"")
                : OperatingSystem.IsMacOS() ? new ProcessStartInfo("open", $"\"{path}\"")
                : new ProcessStartInfo("xdg-open", $"\"{path}\"");
            info.UseShellExecute = false;
            Process.Start(info)?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nessun file manager disponibile: non è un motivo per interrompere l'utente.
        }
    }
}
