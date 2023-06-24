using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

    public Process? Run(FileInfo runner, bool fakeLogin, bool noPlugins, bool noThirdPlugins, FileInfo gameExe, string gameArgs, IDictionary<string, string> environment, DalamudLoadMethod loadMethod, DalamudStartInfo startInfo)
    {
        var gameExePath = "";
        var dotnetRuntimePath = "";

        Parallel.Invoke(
            () => { gameExePath = compatibility.UnixToWinePath(gameExe.FullName); },
            () => { dotnetRuntimePath = compatibility.UnixToWinePath(dotnetRuntime.FullName); },
            () => { startInfo.LoggingPath = compatibility.UnixToWinePath(startInfo.LoggingPath); },
            () => { startInfo.WorkingDirectory = compatibility.UnixToWinePath(startInfo.WorkingDirectory); },
            () => { startInfo.ConfigurationPath = compatibility.UnixToWinePath(startInfo.ConfigurationPath); },
            () => { startInfo.PluginDirectory = compatibility.UnixToWinePath(startInfo.PluginDirectory); },
            () => { startInfo.AssetDirectory = compatibility.UnixToWinePath(startInfo.AssetDirectory); }
        );

        var prevDalamudRuntime = Environment.GetEnvironmentVariable("DALAMUD_RUNTIME");
        if (string.IsNullOrWhiteSpace(prevDalamudRuntime))
            environment.Add("DALAMUD_RUNTIME", dotnetRuntimePath);

        var launchArguments = new List<string>
        {
            $"\"{runner.FullName}\"",
            DalamudInjectorArgs.LAUNCH,
            DalamudInjectorArgs.Mode(loadMethod == DalamudLoadMethod.EntryPoint ? "entrypoint" : "inject"),
            DalamudInjectorArgs.Game(gameExePath),
            DalamudInjectorArgs.WorkingDirectory(startInfo.WorkingDirectory),
            DalamudInjectorArgs.ConfigurationPath(startInfo.ConfigurationPath),
            DalamudInjectorArgs.LoggingPath(startInfo.LoggingPath),
            DalamudInjectorArgs.PluginDirectory(startInfo.PluginDirectory),
            DalamudInjectorArgs.AssetDirectory(startInfo.AssetDirectory),
            DalamudInjectorArgs.ClientLanguage((int)startInfo.Language),
            DalamudInjectorArgs.DelayInitialize(startInfo.DelayInitializeMs),
            DalamudInjectorArgs.TsPackB64(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(startInfo.TroubleshootingPackData))),
        };

        if (loadMethod == DalamudLoadMethod.ACLonly)
            launchArguments.Add(DalamudInjectorArgs.WITHOUT_DALAMUD);

        if (fakeLogin)
            launchArguments.Add(DalamudInjectorArgs.FAKE_ARGUMENTS);

        if (noPlugins)
            launchArguments.Add(DalamudInjectorArgs.NO_PLUGIN);

        if (noThirdPlugins)
            launchArguments.Add(DalamudInjectorArgs.NO_THIRD_PARTY);

        launchArguments.Add("--");
        launchArguments.Add(gameArgs);

        var dalamudProcess = compatibility.RunInPrefix(string.Join(" ", launchArguments), environment: environment, redirectOutput: true, writeLog: true);
        var output = dalamudProcess.StandardOutput.ReadLine();

        if (output == null)
            throw new DalamudRunnerException("An internal Dalamud error has occured");

        Console.WriteLine(output);

        new Thread(() =>
        {
            while (!dalamudProcess.StandardOutput.EndOfStream)
            {
                var output = dalamudProcess.StandardOutput.ReadLine();
                if (output != null)
                    Console.WriteLine(output);
            }

        }).Start();

        try
        {
            var dalamudConsoleOutput = JsonConvert.DeserializeObject<DalamudConsoleOutput>(output);
            var unixPid = compatibility.GetUnixProcessId(dalamudConsoleOutput.Pid);

            if (unixPid == 0)
            {
                Log.Error("Could not retrive Unix process ID, this feature currently requires a patched wine version");
                return null;
            }

            var gameProcess = Process.GetProcessById(unixPid);
            Log.Verbose($"Got game process handle {gameProcess.Handle} with Unix pid {gameProcess.Id} and Wine pid {dalamudConsoleOutput.Pid}");
            return gameProcess;
        }
        catch (JsonReaderException ex)
        {
            Log.Error(ex, $"Couldn't parse Dalamud output: {output}");
            return null;
        }
    }
}