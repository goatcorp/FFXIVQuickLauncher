using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public enum DistroPackage
{
    ubuntu,

    fedora,

    arch,

    none,
}

public static class UnixHelpers
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
    public static bool DxvkHudStringIsValid(string customHud)
    {
        var ALLOWED_CHARS = "^[0-9a-zA-Z,=.]+$";
        var ALLOWED_WORDS = "^(?:devinfo|fps|frametimes|submissions|drawcalls|pipelines|descriptors|memory|gpuload|version|api|cs|compiler|samplers|scale=(?:[0-9])*(?:.(?:[0-9])+)?)$";

        if (string.IsNullOrWhiteSpace(customHud)) return false;
        if (customHud == "full") return true;
        if (customHud == "1") return true;
        if (!Regex.IsMatch(customHud, ALLOWED_CHARS)) return false;

        string[] hudvars = customHud.Split(",");

        return hudvars.All(hudvar => Regex.IsMatch(hudvar, ALLOWED_WORDS));        
    }

    public static bool MangoHudIsInstalled()
    {
        var usrLib = Path.Combine("/usr", "lib", "mangohud", "libMangoHud.so"); // fedora uses this
        var usrLib64 = Path.Combine("/usr", "lib64", "mangohud", "libMangoHud.so"); // arch and openSUSE use this
        var flatpak = Path.Combine(new string[] { "/usr", "lib", "extensions", "vulkan", "MangoHud", "lib", "x86_64-linux-gnu", "libMangoHud.so"});
        var debuntu = Path.Combine(new string[] { "/usr", "lib", "x86_64-linux-gnu", "mangohud", "libMangoHud.so"});
        if (File.Exists(usrLib64) || File.Exists(usrLib) || File.Exists(flatpak) || File.Exists(debuntu))
            return true;
        return false;
    }
}