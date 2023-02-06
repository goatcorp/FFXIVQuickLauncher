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

    public bool IsToolReady { get; private set; }

    public WineSettings Settings { get; private set; }

    private string WineBinPath => Settings.StartupType == WineStartupType.Custom ?
                                    Settings.CustomBinPath :
                                    Path.Combine(toolDirectory.FullName, Settings.WineFolder, "bin");
    
    private string Wine64Path => Path.Combine(WineBinPath, "wine64");
    private string WineServerPath => Path.Combine(WineBinPath, "wineserver");
    private string SteamPath => Settings.SteamPath;
    private string ProtonPath => Path.Combine(Settings.ProtonPath,"proton");

    public bool IsToolDownloaded => File.Exists(Wine64Path) && Settings.Prefix.Exists;

    public DxvkSettings DxvkSettings { get; private set; }

    private readonly bool gamemodeOn;

    public bool useProton => Settings.StartupType == WineStartupType.Proton;

    public CompatibilityTools(WineSettings wineSettings, DxvkSettings dxvkSettings, bool? gamemodeOn, DirectoryInfo toolsFolder)
    {
        this.Settings = wineSettings;
        this.DxvkSettings = dxvkSettings;
        this.gamemodeOn = gamemodeOn ?? false;
        this.toolDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "beta"));
        this.dxvkDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "dxvk"));

        this.logWriter = new StreamWriter(wineSettings.LogFile.FullName);

        if (Settings.StartupType != WineStartupType.Custom && !useProton)
        {
            if (!this.toolDirectory.Exists)
                this.toolDirectory.Create();

            if (!this.dxvkDirectory.Exists)
                this.dxvkDirectory.Create();
        }

        if (!Settings.Prefix.Exists) Settings.Prefix.Create();
        if (!Settings.ProtonPrefix.Exists) Settings.ProtonPrefix.Create();
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        if (!useProton)
        {
            if (!File.Exists(Wine64Path))
            {
                Log.Information("Compatibility tool does not exist, downloading");
                await DownloadTool(tempPath).ConfigureAwait(false);
            }

            EnsurePrefix();
            await Dxvk.InstallDxvk(Settings.Prefix, dxvkDirectory, DxvkSettings).ConfigureAwait(false);
        }
        else
            EnsurePrefix();

        IsToolReady = true;
    }

    private async Task DownloadTool(DirectoryInfo tempPath)
    {
        using var client = new HttpClient();
        var tempFilePath = Path.Combine(tempPath.FullName, $"{Guid.NewGuid()}");

        await File.WriteAllBytesAsync(tempFilePath, await client.GetByteArrayAsync(Settings.WineURL).ConfigureAwait(false)).ConfigureAwait(false);

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
        ProcessStartInfo psi;
        if (useProton)
        {
            psi = new ProcessStartInfo(ProtonPath);
            psi.Arguments = "runinprefix " + command;
        }
        else
        {
            psi = new ProcessStartInfo(Wine64Path);
            psi.Arguments = command;
        }

        Log.Verbose($"Running in prefix: {psi.FileName} {psi.Arguments}");
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        ProcessStartInfo psi;
        if (useProton)
        {
            psi = new ProcessStartInfo(ProtonPath);
            psi.ArgumentList.Add("runinprefix");
        }
        else
        {
            psi = new ProcessStartInfo(Wine64Path);
        }
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
        wineEnviromentVariables.Add("WINEDLLOVERRIDES", $"msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi={(DxvkSettings.Enabled && !wineD3D ? "n" : "b")}");

        if (useProton)
        {
            wineEnviromentVariables.Add("STEAM_COMPAT_DATA_PATH", Settings.ProtonPrefix.FullName);
            wineEnviromentVariables.Add("STEAM_COMPAT_CLIENT_INSTALL_PATH", SteamPath);
            wineEnviromentVariables.Add("PROTON_LOG", "1");
            wineEnviromentVariables.Add("PROTON_LOG_DIR", Path.Combine(Settings.ProtonPrefix.Parent.FullName, "logs"));
            foreach (KeyValuePair<string, string> dxvkVar in DxvkSettings.DxvkVars)
            {
                
                if (dxvkVar.Key == "DXVK_CONFIG_FILE")
                    wineEnviromentVariables.Add(dxvkVar.Key, Path.Combine(Settings.ProtonPrefix.FullName,"dxvk.conf"));
                else if (dxvkVar.Key == "DXVK_STATE_CACHE_PATH")
                { } // Do nothing. Let Proton manage this.
                else
                    wineEnviromentVariables.Add(dxvkVar.Key, dxvkVar.Value);
            }
            if (!Settings.FsyncOn) wineEnviromentVariables.Add("PROTON_NO_FSYNC", "1");
            if (!Settings.EsyncOn && !Settings.FsyncOn) wineEnviromentVariables.Add("PROTON_NO_FSYNC", "1");
        }
        else
        {
            wineEnviromentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);
            foreach (KeyValuePair<string, string> dxvkVar in DxvkSettings.DxvkVars)
                wineEnviromentVariables.Add(dxvkVar.Key, dxvkVar.Value);
            if (Settings.EsyncOn) wineEnviromentVariables.Add("WINEESYNC", "1");
            if (Settings.FsyncOn) wineEnviromentVariables.Add("WINEFSYNC", "1");
        }

        if (!string.IsNullOrEmpty(Settings.DebugVars))
        {
            wineEnviromentVariables.Add("WINEDEBUG", Settings.DebugVars);
        }

        wineEnviromentVariables.Add("XL_WINEONLINUX", "true");
        string ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "";

        if (gamemodeOn && !ldPreload.Contains("libgamemodeauto.so.0"))
        {
            ldPreload = ldPreload.Equals("") ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
        }
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

        if (useProton)
        {
            ProcessStartInfo psifirstrun = new ProcessStartInfo(ProtonPath);
            psifirstrun.RedirectStandardOutput = redirectOutput;
            psifirstrun.RedirectStandardError = writeLog;
            psifirstrun.UseShellExecute = false;
            psifirstrun.WorkingDirectory = workingDirectory;
            psifirstrun.ArgumentList.Add("run");

            MergeDictionaries(psifirstrun.EnvironmentVariables, wineEnviromentVariables);
            MergeDictionaries(psifirstrun.EnvironmentVariables, environment);

            Process firstrun = new();
            firstrun.StartInfo = psifirstrun;

            firstrun.ErrorDataReceived += new DataReceivedEventHandler((_, errLine) =>
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

            firstrun.Start();
        }


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

    public Int32 GetUnixProcessIdByName(string executableName)
    {
        Console.WriteLine($"Process Name = {executableName}");
        ProcessStartInfo psi = new ProcessStartInfo("pgrep");
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(executableName);

        Process pidget = new();
        pidget.StartInfo = psi;
        pidget.Start();

        var output = pidget.StandardOutput.ReadToEnd();
        if (string.IsNullOrWhiteSpace(output))
            return 0;
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var unixPids = matchingLines.Select(l => int.Parse(l, System.Globalization.NumberStyles.Integer)).ToArray();
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
