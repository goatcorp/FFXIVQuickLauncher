using System.Diagnostics;
using Serilog;
using XIVLauncher.Common;

namespace XIVLauncher.Core.Compatibility;

public class CompatibilityTools
{
    private DirectoryInfo toolDirectory;

    private const string WINE_GE_RELEASE_URL = "https://github.com/GloriousEggroll/wine-ge-custom/releases/download/GE-Proton7-8/wine-lutris-GE-Proton7-8-x86_64.tar.xz";
    private const string WINE_GE_RELEASE_NAME = "lutris-GE-Proton7-8-x86_64";

    public string Wine64Path => Path.Combine(toolDirectory.FullName, WINE_GE_RELEASE_NAME, "bin", "wine64");
    public string WineServerPath => Path.Combine(toolDirectory.FullName, WINE_GE_RELEASE_NAME, "bin", "wineserver");

    public DirectoryInfo Prefix { get; private set; }
    public bool IsToolReady { get; private set; }

    public bool IsToolDownloaded => File.Exists(Wine64Path) && this.Prefix.Exists;

    public CompatibilityTools(Storage storage)
    {
        var toolsFolder = storage.GetFolder("compatibilitytool");

        this.toolDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "beta"));
        this.Prefix = storage.GetFolder("wineprefix");

        if (!this.toolDirectory.Exists)
            this.toolDirectory.Create();

        if (!this.Prefix.Exists)
            this.Prefix.Create();
    }

    public async Task EnsureTool()
    {
        if (File.Exists(Wine64Path))
        {
            IsToolReady = true;
            return;
        }

        Log.Information("Compatibility tool does not exist, downloading");

        using var client = new HttpClient();
        var tempPath = Path.GetTempFileName();

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(WINE_GE_RELEASE_URL).ConfigureAwait(false));

        Util.Untar(tempPath, this.toolDirectory.FullName);

        Log.Information("Compatibility tool successfully extracted to {Path}", this.toolDirectory.FullName);

        File.Delete(tempPath);

        EnsurePrefix();
        await Dxvk.InstallDxvk(Prefix).ConfigureAwait(false);

        IsToolReady = true;
    }

    private void ResetPrefix()
    {
        this.Prefix.Refresh();

        if (this.Prefix.Exists)
            this.Prefix.Delete(true);

        this.Prefix.Create();
        EnsurePrefix();
    }

    public void EnsurePrefix()
    {
        RunInPrefix("cmd /c dir %userprofile%/My Documents > nul").WaitForExit();
    }

    public Process? RunInPrefix(string command, string environment = "")
    {
        var line = $"WINEPREFIX=\"{this.Prefix.FullName}\" WINEDLLOVERRIDES=\"mscoree,mshtml=\" {Wine64Path} {command}";

        var psi = new ProcessStartInfo(Util.GetBinaryFromPath("sh"))
        {
            Arguments = $"-c \"{line}\""
        };

        return Process.Start(psi);
    }

    public void Kill()
    {
        var psi = new ProcessStartInfo(Util.GetBinaryFromPath("sh"))
        {
            Arguments = $"-c \"WINEPREFIX=\"{this.Prefix.FullName}\" {WineServerPath} -k\""
        };

        Process.Start(psi);
    }

    public void EnsureGameFixes()
    {
        GameFixes.AddDefaultConfig(this.Prefix);
    }
}
