using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixDalamudRunner : IDalamudRunner
{
    private readonly CompatibilityTools compatibility;
    private readonly DirectoryInfo dotnetRuntime;

    public UnixDalamudRunner(CompatibilityTools compatibility, DirectoryInfo dotnetRuntime)
    {
        this.compatibility = compatibility;
        this.dotnetRuntime = dotnetRuntime;
    }

    public Process? Run(FileInfo runner, bool fakeLogin, FileInfo gameExe, string gameArgs, IDictionary<string, string> environment, DalamudLoadMethod loadMethod, DalamudStartInfo startInfo)
    {
        environment.Add("DALAMUD_RUNTIME", compatibility.UnixToWinePath(dotnetRuntime.FullName));

        var launchArguments = new List<string> { $"\"{runner.FullName}\"", "launch",
            $"--mode={(loadMethod == DalamudLoadMethod.EntryPoint ? "entrypoint" : "inject")}",
            $"--game=\"{compatibility.UnixToWinePath(gameExe.FullName)}\"",
            $"--dalamud-working-directory=\"{compatibility.UnixToWinePath(startInfo.WorkingDirectory)}\"",
            $"--dalamud-configuration-path=\"{compatibility.UnixToWinePath(startInfo.ConfigurationPath)}\"",
            $"--dalamud-plugin-directory=\"{compatibility.UnixToWinePath(startInfo.PluginDirectory)}\"",
            $"--dalamud-dev-plugin-directory=\"{compatibility.UnixToWinePath(startInfo.DefaultPluginDirectory)}\"",
            $"--dalamud-asset-directory=\"{compatibility.UnixToWinePath(startInfo.AssetDirectory)}\"",
            $"--dalamud-client-language={(int)startInfo.Language}",
            $"--dalamud-delay-initialize={startInfo.DelayInitializeMs}"
            };

        if (loadMethod == DalamudLoadMethod.ACLonly)
            launchArguments.Add("--without-dalamud");

        if (fakeLogin)
            launchArguments.Add("--fake-arguments");

        launchArguments.Add("--");
        launchArguments.Add(gameArgs);

        var dalamudProcess = compatibility.RunInPrefix(string.Join(" ", launchArguments), environment: environment, redirectOutput: true);
        var output = dalamudProcess.StandardOutput.ReadLine();

        if (output == null)
            throw new DalamudRunnerException("An internal Dalamud error has occured");

        try
        {
            var dalamudConsoleOutput = JsonConvert.DeserializeObject<DalamudConsoleOutput>(output);
            var unixPid = compatibility.GetUnixProcessId(dalamudConsoleOutput.pid);
            if (unixPid == 0)
            {
                Log.Error("Could not retrive Unix process ID, this feature currently requires a patched wine version");
                return null;
            }
            var gameProcess = Process.GetProcessById(unixPid);
            var handle = gameProcess.Handle;
            return gameProcess;
        }
        catch (JsonReaderException ex)
        {
            Log.Error(ex, $"Couldn't parse Dalamud output: {output}");
            return null;
        }
    }
}