using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public static class ToolDownloader
{
    public static async Task InstallDxvk(DirectoryInfo prefix, DirectoryInfo installDirectory, string folder, string downloadUrl)
    {
        var dxvkPath = Path.Combine(installDirectory.FullName, folder, "x64");
        if (!Directory.Exists(dxvkPath))
        {
            Log.Information("DXVK does not exist, downloading");
            await DownloadTool(installDirectory, downloadUrl).ConfigureAwait(false);
        }

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(dxvkPath);

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }

        // 32-bit files for Directx9.
        var dxvkPath32 = Path.Combine(installDirectory.FullName, folder, "x32");
        if (Directory.Exists(dxvkPath32))
        {
            var syswow64 = Path.Combine(prefix.FullName, "drive_c", "windows", "syswow64");
            files = Directory.GetFiles(dxvkPath32);

            foreach (string fileName in files)
            {
                File.Copy(fileName, Path.Combine(syswow64, Path.GetFileName(fileName)), true);
            }
        }   
    }

    public static async Task InstallWine(DirectoryInfo installDirectory, string folder, string downloadUrl)
    {
        if (!Directory.Exists(Path.Combine(installDirectory.FullName, folder)))
            await DownloadTool(installDirectory, downloadUrl).ConfigureAwait(false);
    }

    private static async Task DownloadTool(DirectoryInfo installDirectory, string downloadUrl)
    {
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        File.Delete(tempPath);
    }

    public enum DxvkHudType
    {
        [SettingsDescription("None", "Show nothing")]
        None,

        [SettingsDescription("FPS", "Only show FPS")]
        Fps,

        [SettingsDescription("Full", "Show everything")]
        Full,
    }
}