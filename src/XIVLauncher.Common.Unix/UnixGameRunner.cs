using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixGameRunner : IGameRunner
{
    public static HashSet<Int32> runningPids = new HashSet<Int32>();

    private readonly WineStartupType startupType;
    private readonly string startupCommandLine;
    private readonly CompatibilityTools compatibility;
    private readonly Dxvk.DxvkHudType hudType;
    private readonly bool gamemodeOn;
    private readonly string dxvkAsyncOn;
    private readonly string esyncOn;
    private readonly string fsyncOn;
    private readonly string wineDebugVars;
    private readonly FileInfo wineLogFile;
    private readonly DalamudLauncher dalamudLauncher;
    private readonly bool dalamudOk;

    public UnixGameRunner(WineStartupType startupType, string startupCommandLine, CompatibilityTools compatibility, Dxvk.DxvkHudType hudType, bool gamemodeOn, bool dxvkAsyncOn, bool esyncOn,
                          bool fsyncOn, string wineDebugVars, FileInfo wineLogFile, DalamudLauncher dalamudLauncher, bool dalamudOk)
    {
        this.startupType = startupType;
        this.startupCommandLine = startupCommandLine;
        this.compatibility = compatibility;
        this.hudType = hudType;
        this.gamemodeOn = gamemodeOn;
        this.dxvkAsyncOn = dxvkAsyncOn ? "1" : "0";
        //this.esyncOn = esyncOn ? "1" : "0";
        this.esyncOn = "0";
        //this.fsyncOn = fsyncOn ? "1" : "0";
        this.fsyncOn = "0";
        this.wineDebugVars = wineDebugVars;
        this.wineLogFile = wineLogFile;
        this.dalamudLauncher = dalamudLauncher;
        this.dalamudOk = dalamudOk;
    }

    public object? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        StreamWriter logWriter = new StreamWriter(wineLogFile.FullName);
        string wineHelperPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Resources", "binaries", "DalamudWineHelper.exe");

        Process helperProcess = new Process();

        helperProcess.StartInfo.RedirectStandardOutput = true;
        helperProcess.StartInfo.RedirectStandardError = true;
        helperProcess.StartInfo.UseShellExecute = false;
        helperProcess.StartInfo.WorkingDirectory = workingDirectory;

        helperProcess.ErrorDataReceived += new DataReceivedEventHandler((sendingProcess, errLine) =>
        {
            if (!String.IsNullOrEmpty(errLine.Data))
            {
                logWriter.WriteLine(errLine.Data);
            }
        });

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
            Log.Information("Gamemode is enabled");
            ldPreload = ldPreload.Equals("") ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
        }

        helperProcess.StartInfo.EnvironmentVariables.Add("DXVK_HUD", dxvkHud);
        helperProcess.StartInfo.EnvironmentVariables.Add("DXVK_ASYNC", dxvkAsyncOn);

        helperProcess.StartInfo.EnvironmentVariables.Add("WINEESYNC", esyncOn);
        helperProcess.StartInfo.EnvironmentVariables.Add("WINEFSYNC", fsyncOn);

        helperProcess.StartInfo.EnvironmentVariables.Add("LD_PRELOAD", ldPreload);
        //Log.Debug("Applying LD_PRELOAD : {LD_PRELOAD}", LD_PRELOAD);

        if (!string.IsNullOrEmpty(this.wineDebugVars))
        {
            helperProcess.StartInfo.EnvironmentVariables.Add("WINEDEBUG", this.wineDebugVars);
        }

        helperProcess.StartInfo.EnvironmentVariables.Add("XL_WINEONLINUX", "true");
        helperProcess.StartInfo.EnvironmentVariables.Add("DALAMUD_RUNTIME", compatibility.UnixToWinePath(compatibility.DotnetRuntime.FullName));

        if (this.startupType == WineStartupType.Managed)
        {
            helperProcess.StartInfo.FileName = compatibility.Wine64Path;

            helperProcess.StartInfo.ArgumentList.Add(wineHelperPath);
            helperProcess.StartInfo.ArgumentList.Add(path);
            helperProcess.StartInfo.ArgumentList.Add(arguments);

            helperProcess.StartInfo.EnvironmentVariables.Add("WINEPREFIX", compatibility.Prefix.FullName);
            helperProcess.StartInfo.EnvironmentVariables.Add("WINEDLLOVERRIDES", "d3d9,d3d11,d3d10core,dxgi,mscoree=n");
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
        Int32 gameProcessId = 0;
        helperProcess.BeginErrorReadLine();
        while (gameProcessId == 0)
        {
            Thread.Sleep(50);
            var allGamePids = new HashSet<Int32>(compatibility.GetProcessIds("ffxiv_dx11.exe"));
            allGamePids.ExceptWith(runningPids);
            gameProcessId = allGamePids.ToArray().FirstOrDefault();
        }
        runningPids.Add(gameProcessId);
        if (this.dalamudOk)
        {
            Log.Verbose("[UnixGameRunner] Now running DLL inject");
            this.dalamudLauncher.Run(gameProcessId);
        }
        return gameProcessId;
    }
}