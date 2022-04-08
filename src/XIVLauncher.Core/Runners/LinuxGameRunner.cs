using System.Diagnostics;
using System.Reflection;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Core.Configuration.Linux;

namespace XIVLauncher.Core.Runners;

public class LinuxGameRunner : IGameRunner
{
    private readonly LinuxStartupType startupType;
    private readonly string startupCommandLine;

    public LinuxGameRunner(LinuxStartupType startupType, string startupCommandLine)
    {
        this.startupType = startupType;
        this.startupCommandLine = startupCommandLine;
    }

    public Process? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        if (this.startupType == LinuxStartupType.Managed)
        {
            throw new NotImplementedException();
        }

        var wineHelperPath = Path.Combine(Assembly.GetExecutingAssembly().Location, "Resources", "DalamudWineHelper.exe");
        var formattedCommand = this.startupCommandLine.Replace("%COMMAND%", $"{wineHelperPath} \"{path}\" \"{arguments}\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = "sh",
            Arguments = $"-c \"{formattedCommand}\"",
            WorkingDirectory = workingDirectory,
        };

        foreach (var variable in environment)
        {
            startInfo.EnvironmentVariables.Add(variable.Key, variable.Value);
        }

        Log.Information("Starting game with command: {Command}", formattedCommand);

        return Process.Start(startInfo);
    }
}