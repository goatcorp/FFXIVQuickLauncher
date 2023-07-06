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

    public WineSettings WineSettings { get; private set; }

    public DxvkSettings DxvkSettings { get; private set; }

    private string WineDLLOverrides;

    private FileInfo LogFile;

    public DirectoryInfo Prefix { get; private set; }

    public bool IsToolDownloaded => File.Exists(WineSettings.RunCommand) && Prefix.Exists;

    public bool IsFlatpak;  // Not currently used.


    public CompatibilityTools(WineSettings wineSettings, DxvkSettings dxvkSettings, DirectoryInfo prefix, DirectoryInfo toolsFolder, FileInfo logfile, bool isFlatpak)
    {
        WineSettings = wineSettings;
        DxvkSettings = dxvkSettings;
        Prefix = prefix;
        var wineoverrides = "msquic=,mscoree=n,b;";
        if (dxvkSettings.IsDxvk)
        {
            wineoverrides += "d3d9,d3d11,d3d10core,dxgi=n,b";
        }
        else
        {
            wineoverrides += "d3d9,d3d11,d3d10core,dxgi=b";
        }
        WineDLLOverrides = wineoverrides;
        wineDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "wine"));
        dxvkDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "dxvk"));
        LogFile = logfile;

        logWriter = new StreamWriter(LogFile.FullName);

        IsFlatpak = isFlatpak;

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
        await EnsureWine();
        if (!File.Exists(WineSettings.RunCommand))
            throw new FileNotFoundException("No wine or wine64 binary could be found.");
        EnsurePrefix();

        // Check to make sure dxvk is valid
        if (DxvkSettings.IsDxvk)
            await EnsureDxvk();

        IsToolReady = true;
        Log.Information($"Using wine at path {WineSettings.RunCommand}");
    }

    private async Task EnsureWine()
    {
        if (!WineSettings.IsManaged) return;

        await DownloadTool(wineDirectory.FullName, WineSettings.Folder, WineSettings.DownloadUrl);
       
        // Use wine if wine64 isn't found. This is mostly for WoW64 wine builds.
        WineSettings.RunCommand = WineSettings.SetWineOrWine64(Path.Combine(wineDirectory.FullName, WineSettings.Folder, "bin"));
    }

    private async Task EnsureDxvk()
    {
        await DownloadTool(dxvkDirectory.FullName, DxvkSettings.Folder, DxvkSettings.DownloadUrl);

        var prefixinstall = Path.Combine(Prefix.FullName, "drive_c", "windows", "system32");
        var files = new DirectoryInfo(Path.Combine(dxvkDirectory.FullName, DxvkSettings.Folder, "x64")).GetFiles();

        foreach (FileInfo fileName in files)
            fileName.CopyTo(Path.Combine(prefixinstall, fileName.Name), true);
    }

    private async Task DownloadTool(string toolDirectory, string toolFolder, string downloadUrl)
    {
        if (IsDirectoryEmpty(Path.Combine(toolDirectory, toolFolder)))
        {
            if (string.IsNullOrEmpty(downloadUrl))
            {
                Log.Error($"Attempted to download {toolFolder} without a download URL.");
                throw new InvalidOperationException($"{toolFolder} does not exist, and no download URL was provided for it.");
            }
            Log.Information($"{toolFolder} does not exist. Downloading...");
            using var client = new HttpClient();
            var tempPath = Path.GetTempFileName();

            File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
            PlatformHelpers.Untar(tempPath, toolDirectory);

            File.Delete(tempPath);
        }        
    }

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
        var psi = new ProcessStartInfo(WineSettings.RunCommand);
        psi.Arguments = command.Trim();

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(WineSettings.RunCommand);
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
        if (wineD3D)
            wineEnvironmentVariables.Add("WINEDLLOVERRIDES", "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=b");
        else
            wineEnvironmentVariables.Add("WINEDLLOVERRIDES", WineDLLOverrides);
        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");

        MergeDictionaries(psi.Environment, WineSettings.Environment);
        MergeDictionaries(psi.Environment, DxvkSettings.Environment);
        MergeDictionaries(psi.Environment, wineEnvironmentVariables);
        MergeDictionaries(psi.Environment, environment);

// #if FLATPAK_NOTRIGHTNOW
//         psi.FileName = "flatpak-spawn";

//         psi.ArgumentList.Insert(0, "--host");
//         psi.ArgumentList.Insert(1, WineSettings.RunCommand);

//         foreach (KeyValuePair<string, string> envVar in wineEnvironmentVariables)
//         {
//             psi.ArgumentList.Insert(1, $"--env={envVar.Key}={envVar.Value}");
//         }

//         if (environment != null)
//         {
//             foreach (KeyValuePair<string, string> envVar in environment)
//             {
//                 psi.ArgumentList.Insert(1, $"--env=\"{envVar.Key}\"=\"{envVar.Value}\"");
//             }
//         }
// #endif

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

    public Int32 GetUnixProcessId(Int32 winePid, string executableName)
    {
        if (winePid == 0)
            return GetUnixProcessIdByName(executableName);
        var wineDbg = RunInPrefix("winedbg --command \"info procmap\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        if (output.Contains("syntax error\n"))
            return GetUnixProcessIdByName(executableName);
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Where(
            l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber) == winePid);
        var unixPids = matchingLines.Select(l => int.Parse(l.Substring(10, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
        var unixPid = unixPids.FirstOrDefault();
        return (unixPid == 0) ? GetUnixProcessIdByName(executableName) : unixPid;
    }

    private Int32 GetUnixProcessIdByName(string executableName)
    {
        int closest = 0;
        int early = 0;
        var currentProcess = Process.GetCurrentProcess(); // Gets XIVLauncher.Core's process
        bool nonunique = false;
        foreach (var process in Process.GetProcessesByName(executableName))
        {
            if (process.Id < currentProcess.Id)
            {
                early = process.Id;
                continue;  // Process was launched before XIVLauncher.Core
            }
            // Assume that the closest PID to XIVLauncher.Core's is the correct one. But log an error if more than one is found.
            if ((closest - currentProcess.Id) > (process.Id - currentProcess.Id) || closest == 0)
            {
                if (closest != 0) nonunique = true;
                closest = process.Id;
            }
            if (nonunique) Log.Error($"More than one {executableName} found! Selecting the most likely match with process id {closest}.");
        }
        // Deal with rare edge-case where pid rollover causes the ffxiv pid to be lower than XLCore's.
        if (closest == 0 && early != 0) closest = early;
        if (closest != 0) Log.Verbose($"Process for {executableName} found using fallback method: {closest}. XLCore pid: {currentProcess.Id}");
        return closest;
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
        var psi = new ProcessStartInfo(WineSettings.WineServer)
        {
            Arguments = "-k"
        };
        psi.Environment.Add("WINEPREFIX", Prefix.FullName);

        Process.Start(psi);
    }

    private bool IsDirectoryEmpty(string folder)
    {
        if (!Directory.Exists(folder)) return true;
        return !Directory.EnumerateFileSystemEntries(folder).Any();
    }
}