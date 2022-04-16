using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixGameRunner : IGameRunner
{
    private readonly WineStartupType startupType;
    private readonly string startupCommandLine;
    private readonly CompatibilityTools compatibility;
    private readonly Dxvk.DxvkHudType hudType;
    private readonly string wineDebugVars;
    private readonly FileInfo wineLogFile;

    public UnixGameRunner(WineStartupType startupType, string startupCommandLine, CompatibilityTools compatibility, Dxvk.DxvkHudType hudType, string wineDebugVars, FileInfo wineLogFile)
    {
        this.startupType = startupType;
        this.startupCommandLine = startupCommandLine;
        this.compatibility = compatibility;
        this.hudType = hudType;
        this.wineDebugVars = wineDebugVars;
        this.wineLogFile = wineLogFile;
    }

    public object? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        StreamWriter logWriter = new StreamWriter(wineLogFile.FullName);
        string wineHelperPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Resources", "binaries", "DalamudWineHelper.exe");

        Process helperProcess = new Process();
        helperProcess.StartInfo.RedirectStandardOutput = true;
        helperProcess.StartInfo.RedirectStandardError = true;

        helperProcess.ErrorDataReceived += new DataReceivedEventHandler((sendingProcess, errLine) =>
        {
            if (!String.IsNullOrEmpty(errLine.Data))
            {
                logWriter.WriteLine(errLine.Data);
            }
        });

        string dxvkHud = hudType switch
        {
            Dxvk.DxvkHudType.None => "0",
            Dxvk.DxvkHudType.Fps => "fps",
            Dxvk.DxvkHudType.Full => "full",
            _ => throw new ArgumentOutOfRangeException()
        };
        helperProcess.StartInfo.EnvironmentVariables.Add("DXVK_HUD", dxvkHud);
        helperProcess.StartInfo.EnvironmentVariables.Add("DXVK_ASYNC", "1");

        if (!string.IsNullOrEmpty(this.wineDebugVars))
        {
            helperProcess.StartInfo.EnvironmentVariables.Add("WINEDEBUG", this.wineDebugVars);
        }

        helperProcess.StartInfo.EnvironmentVariables.Add("WINEPREFIX", compatibility.Prefix.FullName);
        helperProcess.StartInfo.EnvironmentVariables.Add("WINEDLLOVERRIDES", "d3d9,d3d11,d3d10core,dxgi,mscoree=n");

        if (this.startupType == WineStartupType.Managed)
        {
            helperProcess.StartInfo.FileName = compatibility.Wine64Path;
            helperProcess.StartInfo.ArgumentList.Add(wineHelperPath);
            helperProcess.StartInfo.ArgumentList.Add(path);
            helperProcess.StartInfo.ArgumentList.Add(arguments);
        }
        else
        {
            string formattedCommand = this.startupCommandLine.Replace("%COMMAND%", $"\"{wineHelperPath}\" \"{path}\" \"{arguments}\"");
            helperProcess.StartInfo.FileName = "sh";
            helperProcess.StartInfo.ArgumentList.Add("-c");
            helperProcess.StartInfo.ArgumentList.Add(formattedCommand);
        }

        foreach (var variable in environment)
        {
            helperProcess.StartInfo.EnvironmentVariables.Add(variable.Key, variable.Value);
        }

        Log.Information("Starting game with: {Command} {Args}", helperProcess.StartInfo.FileName, helperProcess.StartInfo.ArgumentList);

        helperProcess.Start();
        helperProcess.BeginErrorReadLine();

        string pid = helperProcess.StandardOutput.ReadToEnd();

        return int.Parse(pid);
    }
}