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
    private DirectoryInfo wineDirectory;
    private DirectoryInfo dxvkDirectory;

    private StreamWriter logWriter;

    public bool IsToolReady { get; private set; }

    public Runner WineSettings { get; private set; }

    public Runner DxvkSettings { get; private set; }

    private Dictionary<string, string> EnvVars;

    public string WineDLLOverrides;

    private FileInfo LogFile;

    public DirectoryInfo Prefix { get; private set; }

    private string Wine64Path => WineSettings.GetCommand();
    
    private string WineServerPath => WineSettings.GetServer();

    private string WineParameters => WineSettings.GetParameters();

    public bool IsToolDownloaded => File.Exists(Wine64Path) && Prefix.Exists;

    public CompatibilityTools(Runner wineSettings, Runner dxvkSettings, Dictionary<string, string> environment, string wineoverrides, DirectoryInfo prefix, DirectoryInfo toolsFolder, FileInfo logfile)
    {
        WineSettings = wineSettings;
        DxvkSettings = dxvkSettings;
        EnvVars = environment;
        Prefix = prefix;
        WineDLLOverrides = (string.IsNullOrEmpty(wineoverrides)) ? "msquic,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=b": wineoverrides;
        wineDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "wine"));
        dxvkDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "dxvk"));
        LogFile = logfile;

        logWriter = new StreamWriter(LogFile.FullName);

        if (!wineDirectory.Exists)
            wineDirectory.Create();

        if (!dxvkDirectory.Exists)
            dxvkDirectory.Create();

        if (!Prefix.Exists)
            Prefix.Create();
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        // Check to make sure wine is valid
        await WineSettings.Install();
        if (!File.Exists(Wine64Path))
            throw new FileNotFoundException("The wine64 binary was not found.");
        EnsurePrefix();

        // Check to make sure dxvk is valid
        if (DxvkSettings is not null)
            await DxvkSettings.Install();

        IsToolReady = true;
    }

    // private async Task EnsureRunner(Runner runner, DirectoryInfo folder)
    // {
    //     if (runner is null) return;
    //     if (IsDirectoryEmpty(Path.Combine(folder.FullName, runner.Folder)))
    //     {
    //         if (string.IsNullOrEmpty(runner.DownloadUrl))
    //         {
    //             Log.Error($"Attempted to download runner {runner.Folder} without a download Url.");
    //             throw new InvalidOperationException($"{runner.Folder} runner does not exist, and no download URL was provided for it.");
    //         }
    //         Log.Information($"{runner.Folder} does not exist. Downloading...");
    //         using var client = new HttpClient();
    //         var tempPath = Path.GetTempFileName();

    //         File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(runner.DownloadUrl));
    //         PlatformHelpers.Untar(tempPath, folder.FullName);

    //         File.Delete(tempPath);
    //     }
    //     if (folder == dxvkDirectory)
    //         InstallDxvk();
    // }

    // private void InstallDxvk()
    // {
    //     var prefixinstall = new DirectoryInfo(Path.Combine(Prefix.FullName, "drive_c", "windows", "system32"));
    //     var files = new DirectoryInfo(Path.Combine(dxvkDirectory.FullName, DxvkSettings.Folder, "x64")).GetFiles();

    //     foreach (FileInfo fileName in files)
    //     {
    //         fileName.CopyTo(Path.Combine(prefixinstall.FullName, fileName.Name), true);
    //     }
    // }

    // private bool IsDirectoryEmpty(string folder)
    // {
    //     if (!Directory.Exists(folder)) return true;
    //     return !Directory.EnumerateFileSystemEntries(folder).Any();
    // }

    private void ResetPrefix()
    {
        Prefix.Refresh();

        if (Prefix.Exists)
            Prefix.Delete(true);

        Prefix.Create();
        EnsurePrefix();
    }

    public void EnsurePrefix()
    {
        RunInPrefix("cmd /c dir %userprofile%/Documents > nul").WaitForExit();
    }

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
        psi.Arguments = (WineParameters + " " + command).Trim();

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, command);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
        if (!string.IsNullOrEmpty(WineParameters))
            foreach (var param in WineParameters.Split(null))
                psi.ArgumentList.Add(param);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.ArgumentList.Aggregate(string.Empty, (a, b) => a + " " + b));
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    private void MergeDictionaries(IDictionary<string, string> a, IDictionary<string, string> b)
    {
        if (b is null)
            return;

        foreach (var keyValuePair in b)
        {
            if (a.ContainsKey(keyValuePair.Key))
            {
                if (keyValuePair.Key == "LD_PRELOAD")
                    a[keyValuePair.Key] = MergeLDPreload(a[keyValuePair.Key], keyValuePair.Value);
                else
                    a[keyValuePair.Key] = keyValuePair.Value;
            }
            else
                a.Add(keyValuePair.Key, keyValuePair.Value);
        }
    }

    private string MergeLDPreload(string a, string b)
    {
        var alist = a.Split(':');
        var blist = b.Split(':');
        var clist = (System.Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "").Split(':');
        
        var merged = (alist.Union(blist)).Union(clist);

        var ldpreload = "";
        foreach (var item in merged)
            ldpreload += item + ":";
        
        return ldpreload.TrimEnd(':');
    }

    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory, IDictionary<string, string> environment, bool redirectOutput, bool writeLog, bool wineD3D)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = writeLog;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var wineEnvironmentVariables = new Dictionary<string, string>();
        wineEnvironmentVariables.Add("WINEPREFIX", Prefix.FullName);
        wineEnvironmentVariables.Add("WINEDLLOVERRIDES", WineDLLOverrides);
        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");

        MergeDictionaries(psi.Environment, WineSettings.Environment);
        if (DxvkSettings is not null)
            MergeDictionaries(psi.Environment, DxvkSettings.Environment);
        MergeDictionaries(psi.Environment, wineEnvironmentVariables);
        MergeDictionaries(psi.Environment, environment);

#if FLATPAK_NOTRIGHTNOW
        psi.FileName = "flatpak-spawn";

        psi.ArgumentList.Insert(0, "--host");
        psi.ArgumentList.Insert(1, Wine64Path);

        foreach (KeyValuePair<string, string> envVar in wineEnvironmentVariables)
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
        var launchArguments = WineSettings.GetPathParameters(unixPath).Split(null);
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
        psi.Environment.Add("WINEPREFIX", Prefix.FullName);

        Process.Start(psi);
    }
}