using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsGameRunner : IGameRunner
{
    private readonly DalamudLauncher dalamudLauncher;
    private readonly bool dalamudOk;
    private readonly DirectoryInfo dotnetRuntimePath;

    public WindowsGameRunner(DalamudLauncher dalamudLauncher, bool dalamudOk, DirectoryInfo dotnetRuntimePath)
    {
        this.dalamudLauncher = dalamudLauncher;
        this.dalamudOk = dalamudOk;
        this.dotnetRuntimePath = dotnetRuntimePath;
    }

    public Process Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        if (dalamudOk)
        {
            var compat = "RunAsInvoker ";
            compat += dpiAwareness switch {
                DpiAwareness.Aware => "HighDPIAware",
                DpiAwareness.Unaware => "DPIUnaware",
                _ => throw new ArgumentOutOfRangeException()
            };
            environment.Add("__COMPAT_LAYER", compat);

            var prevDalamudRuntime = Environment.GetEnvironmentVariable("DALAMUD_RUNTIME");
            if (string.IsNullOrWhiteSpace(prevDalamudRuntime))
                environment.Add("DALAMUD_RUNTIME", dotnetRuntimePath.FullName);

            return this.dalamudLauncher.Run(new FileInfo(path), arguments, environment);
        }
        else
        {
            return NativeAclFix.LaunchGame(workingDirectory, path, arguments, environment, dpiAwareness, process => { });
        }
    }
}