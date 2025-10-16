using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixGameRunner : IGameRunner
{
    private readonly CompatibilityTools compatibility;
    private readonly DalamudLauncher dalamudLauncher;
    private readonly bool dalamudOk;

    public UnixGameRunner(CompatibilityTools compatibility, DalamudLauncher dalamudLauncher, bool dalamudOk)
    {
        this.compatibility = compatibility;
        this.dalamudLauncher = dalamudLauncher;
        this.dalamudOk = dalamudOk;
    }

    public Process? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        if (dalamudOk)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                environment.Add("XL_PLATFORM", "Linux");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                environment.Add("XL_PLATFORM", "MacOS");
            }
            return this.dalamudLauncher.Run(new FileInfo(path), arguments, environment);
        }
        else
        {
            return compatibility.RunInPrefix($"\"{path}\" {arguments}", workingDirectory, environment, writeLog: true);
        }
    }
}
