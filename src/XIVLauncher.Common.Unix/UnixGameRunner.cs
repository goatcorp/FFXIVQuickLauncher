using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixGameRunner : IGameRunner
{
    public static HashSet<Int32> RunningPids = new HashSet<Int32>();

    private readonly CompatibilityTools compatibility;
    private readonly DalamudLauncher dalamudLauncher;
    private readonly bool dalamudOk;
    private readonly DalamudLoadMethod loadMethod;
    private readonly DirectoryInfo dotnetRuntime;

    public UnixGameRunner(CompatibilityTools compatibility, DalamudLauncher dalamudLauncher, bool dalamudOk, DalamudLoadMethod? loadMethod, DirectoryInfo dotnetRuntime)
    {
        this.compatibility = compatibility;
        this.dalamudLauncher = dalamudLauncher;
        this.dalamudOk = dalamudOk;
        this.loadMethod = loadMethod ?? DalamudLoadMethod.DllInject;
        this.dotnetRuntime = dotnetRuntime;
    }

    public object? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        var wineHelperPath = Path.Combine(AppContext.BaseDirectory, "Resources", "binaries", "DalamudWineHelper.exe");
        var launchArguments = new string[] { wineHelperPath, path, arguments };

        environment.Add("DALAMUD_RUNTIME", compatibility.UnixToWinePath(dotnetRuntime.FullName));
        var process = compatibility.RunInPrefix(launchArguments, workingDirectory, environment);

        Int32 gameProcessId = 0;

        while (gameProcessId == 0)
        {
            Thread.Sleep(50);
            var allGamePids = new HashSet<Int32>(compatibility.GetProcessIds("ffxiv_dx11.exe"));
            allGamePids.ExceptWith(RunningPids);
            gameProcessId = allGamePids.ToArray().FirstOrDefault();
        }

        RunningPids.Add(gameProcessId);

        if (this.dalamudOk)
        {
            Log.Verbose("[UnixGameRunner] Now running DLL inject");
            this.dalamudLauncher.Run(gameProcessId);
        }

        return gameProcessId;
    }
}