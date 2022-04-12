using System.Collections.Generic;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsGameRunner : IGameRunner
{
    private readonly DalamudLauncher dalamudLauncher;
    private readonly bool dalamudOk;
    private readonly DalamudLoadMethod loadMethod;

    public WindowsGameRunner(DalamudLauncher dalamudLauncher, bool dalamudOk, DalamudLoadMethod loadMethod)
    {
        this.dalamudLauncher = dalamudLauncher;
        this.dalamudOk = dalamudOk;
        this.loadMethod = loadMethod;
    }

    public object? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        var gameProcess = NativeAclFix.LaunchGame(workingDirectory, path, arguments, environment, dpiAwareness, process =>
        {
            if (this.dalamudOk && this.loadMethod == DalamudLoadMethod.EntryPoint)
            {
                Log.Verbose("[WindowsGameRunner] Now running OEP rewrite");
                this.dalamudLauncher.Run(process);
            }
        });

        if (this.dalamudOk && this.loadMethod == DalamudLoadMethod.DllInject)
        {
            Log.Verbose("[WindowsGameRunner] Now running DLL inject");
            this.dalamudLauncher.Run(gameProcess);
        }

        return gameProcess;
    }
}