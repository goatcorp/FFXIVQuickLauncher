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
    private DirectoryInfo gameConfigDirectory;

    private readonly WineStartupType startupType;
    private readonly string customWineBinPath;

    private const string WINE_TKG_RELEASE_URL = "https://github.com/Kron4ek/Wine-Builds/releases/download/7.6/wine-7.6-staging-tkg-amd64.tar.xz";
    private const string WINE_TKG_RELEASE_NAME = "wine-7.6-staging-tkg-amd64";

    private string WineBinPath => startupType == WineStartupType.Managed ?
                                    Path.Combine(toolDirectory.FullName, WINE_TKG_RELEASE_NAME, "bin") :
                                    customWineBinPath;
    public string Wine64Path => Path.Combine(WineBinPath, "wine64");
    public string WineServerPath => Path.Combine(WineBinPath, "wineserver");

    private readonly string wineDebugVars;
    private readonly FileInfo wineLogFile;

    public DirectoryInfo Prefix { get; private set; }
    public DirectoryInfo DotnetRuntime { get; private set; }
    public bool IsToolReady { get; private set; }

    public bool IsToolDownloaded => File.Exists(Wine64Path) && this.Prefix.Exists;

    private readonly Dxvk.DxvkHudType hudType;

    public CompatibilityTools(WineStartupType? startupType, string customWineBinPath, Storage storage,
        Dxvk.DxvkHudType hudType, string wineDebugVars, FileInfo wineLogFile, DirectoryInfo configDirectory)
    {
        this.startupType = startupType ?? WineStartupType.Managed;
        this.customWineBinPath = customWineBinPath;
        this.hudType = hudType;
        this.wineDebugVars = wineDebugVars;
        this.wineLogFile = wineLogFile;

        var toolsFolder = storage.GetFolder("compatibilitytool");

        this.toolDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "beta"));
        this.gameConfigDirectory = configDirectory;
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

        File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(WINE_TKG_RELEASE_URL).ConfigureAwait(false));

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
        var logWriter = new StreamWriter(wineLogFile.FullName);
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var wineEnviromentVariables = new Dictionary<string, string>();
        wineEnviromentVariables.Add("WINEPREFIX", this.Prefix.FullName);
        wineEnviromentVariables.Add("WINEDLLOVERRIDES", "d3d9,d3d11,d3d10core,dxgi,mscoree=n");
        if (!string.IsNullOrEmpty(this.wineDebugVars))
        {
            wineEnviromentVariables.Add("WINEDEBUG", this.wineDebugVars);
        }

        wineEnviromentVariables.Add("XL_WINEONLINUX", "true");

        string dxvkHud = hudType switch
        {
            Dxvk.DxvkHudType.None => "0",
            Dxvk.DxvkHudType.Fps => "fps",
            Dxvk.DxvkHudType.Full => "full",
            _ => throw new ArgumentOutOfRangeException()
        };
        wineEnviromentVariables.Add("DXVK_HUD", dxvkHud);
        wineEnviromentVariables.Add("DXVK_ASYNC", "1");

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
        psi.EnvironmentVariables.Add("WINEPREFIX", this.Prefix.FullName);

        Process.Start(psi);
    }

    public void EnsureGameFixes()
    {
        EnsurePrefix();
        GameFixes.AddDefaultConfig(gameConfigDirectory);
    }
}
