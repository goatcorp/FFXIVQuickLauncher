using System.Diagnostics;
using System.Reflection;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Core.Compatibility;
using XIVLauncher.Core.Configuration.Linux;

namespace XIVLauncher.Core.Runners;

public class LinuxGameRunner : IGameRunner
{
    private readonly LinuxStartupType startupType;
    private readonly string startupCommandLine;
    private readonly CompatibilityTools compatibility;
    private readonly Dxvk.DxvkHudType hudType;

    public LinuxGameRunner(LinuxStartupType startupType, string startupCommandLine, CompatibilityTools compatibility, Dxvk.DxvkHudType hudType)
    {
        this.startupType = startupType;
        this.startupCommandLine = startupCommandLine;
        this.compatibility = compatibility;
        this.hudType = hudType;
    }

    public int? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        var dxvkHud = hudType switch
        {
            Dxvk.DxvkHudType.None => "0",
            Dxvk.DxvkHudType.Fps => "fps",
            Dxvk.DxvkHudType.Full => "full",
            _ => throw new ArgumentOutOfRangeException()
        };

        var wineHelperPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Resources", "binaries", "DalamudWineHelper.exe");
        var defaultEnv = $"WINEPREFIX=\"{compatibility.Prefix}\" DXVK_HUD={dxvkHud} WINEDLLOVERRIDES=\"d3d9,d3d11,d3d10core,dxgi,mscoree=n\"";

        ProcessStartInfo startInfo = new ProcessStartInfo(Util.GetBinaryFromPath("sh"))
        {
            RedirectStandardOutput = true,
        };

        if (this.startupType == LinuxStartupType.Managed)
        {
            startInfo.Arguments = $"-c \"{defaultEnv} {compatibility.Wine64Path} {wineHelperPath} \"{path}\" \"{arguments}\"\"";
        }
        else
        {
            var formattedCommand = this.startupCommandLine.Replace("%COMMAND%", $"{defaultEnv} {wineHelperPath} \"{path}\" \"{arguments}\"");

            startInfo.Arguments = $"-c \"{formattedCommand}\"";
        }

        foreach (var variable in environment)
        {
            startInfo.EnvironmentVariables.Add(variable.Key, variable.Value);
        }

        Log.Information("Starting game with: {Command}", startInfo.Arguments);

        var helperProcess = Process.Start(startInfo);

        var pid = helperProcess.StandardOutput.ReadToEnd();

        return int.Parse(pid);
    }
}
