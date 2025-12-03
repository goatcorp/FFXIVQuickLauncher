using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

#if FLATPAK
#warning THIS IS A FLATPAK BUILD!!!
#endif

namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatibilityTools
{
    private DirectoryInfo toolDirectory;
    private DirectoryInfo dxvkDirectory;

    private StreamWriter logWriter;

#if WINE_XIV_ARCH_LINUX
    private const string WINE_XIV_RELEASE_URL = "https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-arch-8.5.r4.g4211bac7.tar.xz";
#elif WINE_XIV_FEDORA_LINUX
    private const string WINE_XIV_RELEASE_URL = "https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-fedora-8.5.r4.g4211bac7.tar.xz";
#else
    private const string WINE_XIV_RELEASE_URL = "https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-ubuntu-8.5.r4.g4211bac7.tar.xz";
#endif
    private const string WINE_XIV_RELEASE_NAME = "wine-xiv-staging-fsync-git-8.5.r4.g4211bac7";

    public bool IsToolReady { get; private set; }

    public WineSettings Settings { get; private set; }

    private string WineBinPath => Settings.StartupType == WineStartupType.Managed ?
                                    Path.Combine(toolDirectory.FullName, WINE_XIV_RELEASE_NAME, "bin") :
                                    Settings.CustomBinPath;
    private string Wine64Path => Path.Combine(WineBinPath, "wine64");
    private string WineServerPath => Path.Combine(WineBinPath, "wineserver");

    public bool IsToolDownloaded => File.Exists(Wine64Path) && Settings.Prefix.Exists;

    private readonly Dxvk.DxvkHudType hudType;
    private readonly bool gamemodeOn;
    private readonly string dxvkAsyncOn;

    public CompatibilityTools(WineSettings wineSettings, Dxvk.DxvkHudType hudType, bool? gamemodeOn, bool? dxvkAsyncOn, DirectoryInfo toolsFolder)
    {
        this.Settings = wineSettings;
        this.hudType = hudType;
        this.gamemodeOn = gamemodeOn ?? false;
        this.dxvkAsyncOn = (dxvkAsyncOn ?? false) ? "1" : "0";

        this.toolDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "beta"));
        this.dxvkDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "dxvk"));

        this.logWriter = new StreamWriter(wineSettings.LogFile.FullName);

        if (wineSettings.StartupType == WineStartupType.Managed)
        {
            if (!this.toolDirectory.Exists)
                this.toolDirectory.Create();

            if (!this.dxvkDirectory.Exists)
                this.dxvkDirectory.Create();
        }

        if (!wineSettings.Prefix.Exists)
            wineSettings.Prefix.Create();
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        if (!File.Exists(Wine64Path))
        {
            Log.Information("Compatibility tool does not exist, downloading");
            await DownloadTool(tempPath).ConfigureAwait(false);
        }

        EnsurePrefix();
        await Dxvk.InstallDxvk(Settings.Prefix, dxvkDirectory).ConfigureAwait(false);

        IsToolReady = true;
    }

    private async Task DownloadTool(DirectoryInfo tempPath)
    {
        using var client = new HttpClient();
        var tempFilePath = Path.Combine(tempPath.FullName, $"{Guid.NewGuid()}");

        await File.WriteAllBytesAsync(tempFilePath, await client.GetByteArrayAsync(WINE_XIV_RELEASE_URL).ConfigureAwait(false)).ConfigureAwait(false);

        PlatformHelpers.Untar(tempFilePath, this.toolDirectory.FullName);

        Log.Information("Compatibility tool successfully extracted to {Path}", this.toolDirectory.FullName);

        File.Delete(tempFilePath);
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

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
        psi.Arguments = command;

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, command);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.ArgumentList.Aggregate(string.Empty, (a, b) => a + " " + b));
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
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

    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory, IDictionary<string, string> environment, bool redirectOutput, bool writeLog, bool wineD3D)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = writeLog;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var wineEnviromentVariables = new Dictionary<string, string>();
        wineEnviromentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);
        
        // DXMT requires d3d11=n,dxgi=n (both native)
        // DXVK requires d3d11=n,dxgi=b (native d3d11, builtin dxgi)
        // Re-read environment variable each time to catch backend changes
        string xlDxmtValue = Environment.GetEnvironmentVariable("XL_DXMT_ENABLED") ?? "0";
        bool dxmtEnabled = xlDxmtValue == "1";
        string dxgiOverride = wineD3D ? "b" : (dxmtEnabled ? "n" : "b");
        string d3d11Override = wineD3D ? "b" : "n";  // Always use native d3d11 for DXMT/DXVK
        
        Log.Information($"[DXMT Debug] XL_DXMT_ENABLED={xlDxmtValue}, dxmtEnabled={dxmtEnabled}, wineD3D={wineD3D}, d3d11Override={d3d11Override}, dxgiOverride={dxgiOverride}");
        
        wineEnviromentVariables.Add("WINEDLLOVERRIDES", $"msquic=,mscoree=n,b;d3d9,d3d10core={d3d11Override};d3d11={d3d11Override};dxgi={dxgiOverride}");

        if (!string.IsNullOrEmpty(Settings.DebugVars))
        {
            wineEnviromentVariables.Add("WINEDEBUG", Settings.DebugVars);
        }

        wineEnviromentVariables.Add("XL_WINEONLINUX", "true");
        string ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "";

        // Read DXVK_HUD from environment variable set by Swift (Wine.setup())
        // This allows the user to configure HUD options in the settings UI
        string dxvkHud = Environment.GetEnvironmentVariable("DXVK_HUD") ?? "0";

        if (this.gamemodeOn == true && !ldPreload.Contains("libgamemodeauto.so.0"))
        {
            ldPreload = ldPreload.Equals("") ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
        }

        wineEnviromentVariables.Add("DXVK_HUD", dxvkHud);
        // Read DXVK_ASYNC and DXVK_FRAME_RATE from environment (set by Swift Wine.setup())
        string dxvkAsync = Environment.GetEnvironmentVariable("DXVK_ASYNC") ?? "0";
        string dxvkFrameRate = Environment.GetEnvironmentVariable("DXVK_FRAME_RATE") ?? "0";
        wineEnviromentVariables.Add("DXVK_ASYNC", dxvkAsync);
        wineEnviromentVariables.Add("DXVK_FRAME_RATE", dxvkFrameRate);
        wineEnviromentVariables.Add("WINEESYNC", Settings.EsyncOn);
        wineEnviromentVariables.Add("WINEFSYNC", Settings.FsyncOn);

        wineEnviromentVariables.Add("LD_PRELOAD", ldPreload);

        MergeDictionaries(psi.EnvironmentVariables, wineEnviromentVariables);
        MergeDictionaries(psi.EnvironmentVariables, environment);

#if FLATPAK_NOTRIGHTNOW
        psi.FileName = "flatpak-spawn";

        psi.ArgumentList.Insert(0, "--host");
        psi.ArgumentList.Insert(1, Wine64Path);

        foreach (KeyValuePair<string, string> envVar in wineEnviromentVariables)
        {
            psi.ArgumentList.Insert(1, $"--env={envVar.Key}={envVar.Value}");
        }

        if (environment != null)
        {
            foreach (KeyValuePair<string, string> envVar in environment)
            {
                psi.ArgumentList.Insert(1, $"--env=\"{envVar.Key}\"=\"{envVar.Value}\"");
            }
        }
#endif

        Process helperProcess = new();
        helperProcess.StartInfo = psi;
        helperProcess.ErrorDataReceived += new DataReceivedEventHandler((_, errLine) =>
        {
            if (String.IsNullOrEmpty(errLine.Data))
                return;

            try
            {
                logWriter.WriteLine(errLine.Data);
                Console.Error.WriteLine(errLine.Data);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException ||
                                       ex is OverflowException ||
                                       ex is IndexOutOfRangeException)
            {
                // very long wine log lines get chopped off after a (seemingly) arbitrary limit resulting in strings that are not null terminated
                //logWriter.WriteLine("Error writing Wine log line:");
                //logWriter.WriteLine(ex.Message);
            }
        });

        helperProcess.Start();
        if (writeLog)
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

    public Int32 GetUnixProcessId(Int32 winePid)
    {
        var wineDbg = RunInPrefix("winedbg --command \"info procmap\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        if (output.Contains("syntax error\n"))
            return 0;
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Where(
            l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber) == winePid);
        var unixPids = matchingLines.Select(l => int.Parse(l.Substring(10, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
        return unixPids.FirstOrDefault();
    }

    public string UnixToWinePath(string unixPath)
    {
        var launchArguments = new string[] { "winepath", "--windows", unixPath };
        var winePath = RunInPrefix(launchArguments, redirectOutput: true);
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
}