using XIVLauncher.Common;

namespace XIVLauncher.Core.Compatibility;

public static class Dxvk
{
    private const string DXVK_DOWNLOAD = "https://github.com/Sporif/dxvk-async/releases/download/1.10.1/dxvk-async-1.10.1.tar.gz";
    private const string DXVK_NAME = "dxvk-async-1.10.1";

    public static async Task InstallDxvk(DirectoryInfo prefix)
    {
        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();
        var tempFolder = Path.GetTempPath();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(DXVK_DOWNLOAD));
        Util.Untar(tempPath, tempFolder);

        var system32 = Path.Combine(prefix.FullName, "drive_c", "windows", "system32");
        var files = Directory.GetFiles(Path.Combine(tempFolder, DXVK_NAME, "x64"));

        foreach (string fileName in files)
        {
            File.Copy(fileName, Path.Combine(system32, Path.GetFileName(fileName)), true);
        }

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
