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

    public DirectoryInfo Prefix { get; private set; }
    public bool IsToolReady { get; private set; }

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

    public async Task EnsureToolExists()
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

        var tarPath = Util.GetBinaryFromPath("tar");

        var psi = new ProcessStartInfo(tarPath)
        {
            Arguments = $"-xf \"{tempPath}\" -C \"{this.toolDirectory.FullName}\""
        };

        var tarProcess = Process.Start(psi);

        if (tarProcess == null)
            throw new Exception("Could not start tar.");

        tarProcess.WaitForExit();

        if (tarProcess.ExitCode != 0)
            throw new Exception("Could not untar compatibility tool");

        Log.Information("Compatibility tool successfully extracted to {Path}", this.toolDirectory.FullName);

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

    private Process? RunInPrefix(string command, string environment = "")
    {
        var line = $"WINEPREFIX=\"{this.Prefix.FullName}\" DXVK_HUD=full {Wine64Path} {command}";

        var psi = new ProcessStartInfo(Util.GetBinaryFromPath("sh"))
        {
            Arguments = $"-c \"{line}\""
        };

        return Process.Start(psi);
    }

    public void EnsureGameFixes()
    {
        GameFixes.AddDefaultConfig(this.Prefix);
    }
}
