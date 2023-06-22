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

    public bool IsToolDownloaded => File.Exists(WineSettings.RunCommand) && Prefix.Exists;

    public CompatibilityTools(Runner wineSettings, Runner dxvkSettings, Dictionary<string, string> environment, string wineoverrides, DirectoryInfo prefix, DirectoryInfo toolsFolder, FileInfo logfile)
    {
        WineSettings = wineSettings;
        DxvkSettings = dxvkSettings;
        EnvVars = environment;
        Prefix = prefix;
        WineDLLOverrides = (string.IsNullOrEmpty(wineoverrides)) ? "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=n,b" : wineoverrides;
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
        if (!File.Exists(WineSettings.RunCommand))
            throw new FileNotFoundException("The wine64 binary was not found.");
        EnsurePrefix();

        // Check to make sure dxvk is valid
        if (DxvkSettings.RunnerType == "Dxvk")
            await DxvkSettings.Install();

        IsToolReady = true;
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
        psi.Arguments = (WineSettings.RunArguments + " " + command).Trim();

        Log.Verbose("Running in prefix (string): {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(WineSettings.RunCommand);
        if (!string.IsNullOrEmpty(WineSettings.RunArguments))
            foreach (var param in WineSettings.RunArguments.Split(null))
                psi.ArgumentList.Add(param);
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);
        
        Log.Information("Running in prefix (array): {FileName} {Arguments}", psi.FileName, psi.ArgumentList.Aggregate(string.Empty, (a, b) => a + " " + b));
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
        if (wineD3D)
            wineEnvironmentVariables.Add("WINEDLLOVERRIDES", "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=b");
        else
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
        psi.ArgumentList.Insert(1, WineSettings.RunCommand);

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
        var psi = new ProcessStartInfo(WineSettings.PathCommand);
        psi.Arguments = WineSettings.PathArguments + " " + unixPath;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        foreach (var envvar in WineSettings.Environment)
            psi.Environment.Add(envvar.Key, envvar.Value);
        var winePath = Process.Start(psi);
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
        var psi = new ProcessStartInfo(WineSettings.Server)
        {
            Arguments = "-k"
        };
        psi.Environment.Add("WINEPREFIX", Prefix.FullName);

        Process.Start(psi);
    }
}