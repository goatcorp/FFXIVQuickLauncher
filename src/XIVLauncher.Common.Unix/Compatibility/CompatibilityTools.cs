using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

    private StreamWriter logWriter;

    private const string WINE_TKG_RELEASE_URL = "https://github.com/Kron4ek/Wine-Builds/releases/download/7.6/wine-7.6-staging-tkg-amd64.tar.xz";
    private const string WINE_TKG_RELEASE_NAME = "wine-7.6-staging-tkg-amd64";

    public bool IsToolReady { get; private set; }

    public WineSettings Settings { get; private set; }

    private string WineBinPath => Settings.StartupType == WineStartupType.Managed ?
                                    Path.Combine(toolDirectory.FullName, WINE_TKG_RELEASE_NAME, "bin") :
                                    Settings.CustomBinPath;
    private string Wine64Path => Path.Combine(WineBinPath, "wine64");
    private string WineServerPath => Path.Combine(WineBinPath, "wineserver");

    public bool IsToolDownloaded => File.Exists(Wine64Path) && Settings.Prefix.Exists;

    private readonly Dxvk.DxvkHudType hudType;
    private readonly bool gamemodeOn;
    private readonly string dxvkAsyncOn;
    private readonly string esyncOn;
    private readonly string fsyncOn;

    public CompatibilityTools(WineSettings wineSettings, Dxvk.DxvkHudType hudType, bool? gamemodeOn, bool? dxvkAsyncOn, bool? esyncOn, bool? fsyncOn, DirectoryInfo toolsFolder)
    {
        this.Settings = wineSettings;
        this.hudType = hudType;
        this.gamemodeOn = gamemodeOn ?? false;
        this.dxvkAsyncOn = (dxvkAsyncOn ?? false) ? "1" : "0";
        this.esyncOn = (esyncOn ?? false) ? "1" : "0";
        this.fsyncOn = (fsyncOn ?? false) ? "1" : "0";

        this.toolDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "beta"));

        this.logWriter = new StreamWriter(wineSettings.LogFile.FullName);

        if (!this.toolDirectory.Exists)
            this.toolDirectory.Create();

        if (!wineSettings.Prefix.Exists)
            wineSettings.Prefix.Create();
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

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(WINE_TKG_RELEASE_URL).ConfigureAwait(false));

        Util.Untar(tempPath, this.toolDirectory.FullName);

        Log.Information("Compatibility tool successfully extracted to {Path}", this.toolDirectory.FullName);

        File.Delete(tempPath);

        EnsurePrefix();
        await Dxvk.InstallDxvk(Settings.Prefix).ConfigureAwait(false);

        IsToolReady = true;
    }

    private void ResetPrefix()
    {
        Settings.Prefix.Refresh();

        if (Settings.Prefix.Exists)
            Settings.Prefix.Delete(true);

        Settings.Prefix.Create();
        EnsurePrefix();
    }

    public void EnsurePrefix()
    {
        RunInPrefix("cmd /c dir %userprofile%/Documents > nul").WaitForExit();
    }

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
        psi.Arguments = command;
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput);
    }

    private void MergeDictionaries(StringDictionary a, IDictionary<string, string> b)
    {
        if (b is null)
            return;

        foreach (var keyValuePair in b)
        {
            if (a.ContainsKey(keyValuePair.Key))
                a[keyValuePair.Key] = keyValuePair.Value;
            else
                a.Add(keyValuePair.Key, keyValuePair.Value);
        }
    }

    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var wineEnviromentVariables = new Dictionary<string, string>();
        wineEnviromentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);
        wineEnviromentVariables.Add("WINEDLLOVERRIDES", "d3d9,d3d11,d3d10core,dxgi,mscoree=n");

        if (!string.IsNullOrEmpty(Settings.DebugVars))
        {
            wineEnviromentVariables.Add("WINEDEBUG", Settings.DebugVars);
        }

        wineEnviromentVariables.Add("XL_WINEONLINUX", "true");
        string ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "";

        string dxvkHud = hudType switch
        {
            Dxvk.DxvkHudType.None => "0",
            Dxvk.DxvkHudType.Fps => "fps",
            Dxvk.DxvkHudType.Full => "full",
            _ => throw new ArgumentOutOfRangeException()
        };

        if (this.gamemodeOn == true && !ldPreload.Contains("libgamemodeauto.so.0"))
        {
            ldPreload = ldPreload.Equals("") ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
        }

        wineEnviromentVariables.Add("DXVK_HUD", dxvkHud);
        wineEnviromentVariables.Add("DXVK_ASYNC", dxvkAsyncOn);
        wineEnviromentVariables.Add("WINEESYNC", esyncOn);
        wineEnviromentVariables.Add("WINEFSYNC", fsyncOn);

        wineEnviromentVariables.Add("LD_PRELOAD", ldPreload);

        MergeDictionaries(psi.EnvironmentVariables, wineEnviromentVariables);
        MergeDictionaries(psi.EnvironmentVariables, environment);

        Process helperProcess = new();
        helperProcess.StartInfo = psi; 
        helperProcess.ErrorDataReceived += new DataReceivedEventHandler((_, errLine) => logWriter.WriteLine(errLine.Data));

        helperProcess.Start();
        helperProcess.BeginErrorReadLine();
        return helperProcess;
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

    public string UnixToWinePath(string unixPath)
    {
        var winePath = RunInPrefix($"winepath --windows {unixPath}", redirectOutput: true);
        var output = winePath.StandardOutput.ReadToEnd();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    public void AddRegistryKey(string key, string value, string data)
    {
        var args = new string[] { "reg", "add", key, "/v", value, "/d", data, "/f" };
        var wineProcess = RunInPrefix(args);
        wineProcess.WaitForExit();
    }

    public void Kill()
    {
        var psi = new ProcessStartInfo(WineServerPath)
        {
            Arguments = "-k"
        };
        psi.EnvironmentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);

        Process.Start(psi);
    }

    public void EnsureGameFixes(DirectoryInfo gameConfigDirectory)
    {
        EnsurePrefix();
        GameFixes.AddDefaultConfig(gameConfigDirectory);
    }
}