using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatibilityTools
{
    private DirectoryInfo toolDirectory;

    private const string WINE_GE_RELEASE_URL = "https://github.com/GloriousEggroll/wine-ge-custom/releases/download/GE-Proton7-8/wine-lutris-GE-Proton7-8-x86_64.tar.xz";
    private const string WINE_GE_RELEASE_NAME = "lutris-GE-Proton7-8-x86_64";

    public string Wine64Path => Path.Combine(toolDirectory.FullName, WINE_GE_RELEASE_NAME, "bin", "wine64");
    public string WineServerPath => Path.Combine(toolDirectory.FullName, WINE_GE_RELEASE_NAME, "bin", "wineserver");

    public DirectoryInfo Prefix { get; private set; }
    public DirectoryInfo DotnetRuntime { get; private set; }
    public bool IsToolReady { get; private set; }

    public bool IsToolDownloaded => File.Exists(Wine64Path) && this.Prefix.Exists;

    public CompatibilityTools(Storage storage)
    {
        var toolsFolder = storage.GetFolder("compatibilitytool");

        this.toolDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "beta"));
        this.Prefix = storage.GetFolder("wineprefix");
        this.DotnetRuntime = storage.GetFolder("runtime");

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
        RunInPrefix("cmd /c dir %userprofile%/Documents > nul").WaitForExit();
    }

    public Process? RunInPrefix(string command, Dictionary<string, string> environment = null, bool redirectOutput = false)
    {
        var psi = new ProcessStartInfo(Wine64Path)
        {
            Arguments = command,
            RedirectStandardOutput = redirectOutput
        };
        psi.EnvironmentVariables.Add("WINEPREFIX", this.Prefix.FullName);
        if (environment is not null)
            foreach (var keyValuePair in environment)
                psi.EnvironmentVariables.Add(keyValuePair.Key, keyValuePair.Value);
        else
            psi.EnvironmentVariables.Add("WINEDLLOVERRIDES", "mshtml=");
        return Process.Start(psi);
    }

    public Int32[] GetProcessIds(string executableName)
    {
        var wineDbg = RunInPrefix("winedbg --command \"info proc\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => l.Contains(executableName));
        return matchingLines.Select(l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
    }

    public Int32 GetProcessId(string executableName)
    {
        return GetProcessIds(executableName).FirstOrDefault();
    }

    public string WineToUnixPath(string unixPath)
    {
        var winePath = RunInPrefix($"winepath --windows {unixPath}", redirectOutput: true);
        var output = winePath.StandardOutput.ReadToEnd();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    public void Kill()
    {
        var psi = new ProcessStartInfo(WineServerPath)
        {
            Arguments = "-k"
        };
        psi.EnvironmentVariables.Add("WINEPREFIX", this.Prefix.FullName);

        Process.Start(psi);
    }

    public void EnsureGameFixes()
    {
        EnsurePrefix();
        GameFixes.AddDefaultConfig(this.Prefix);
    }
}
