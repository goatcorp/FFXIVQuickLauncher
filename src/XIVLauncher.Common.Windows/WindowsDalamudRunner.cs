using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsDalamudRunner : IDalamudRunner
{
    public Process? Run(FileInfo runner, bool fakeLogin, FileInfo gameExe, string gameArgs, IDictionary<string, string> environment, DalamudLoadMethod loadMethod, DalamudStartInfo startInfo)
    {
        var handleOwner = Process.GetCurrentProcess().Handle;

        var launchArguments = new List<string> { "launch",
            $"--mode={(loadMethod == DalamudLoadMethod.EntryPoint ? "entrypoint" : "inject")}",
            $"--handle-owner={(long)handleOwner}",
            $"--game='{gameExe.FullName}'",
            $"--dalamud-working-directory='{startInfo.WorkingDirectory}'",
            $"--dalamud-configuration-path='{startInfo.ConfigurationPath}'",
            $"--dalamud-plugin-directory='{startInfo.PluginDirectory}'",
            $"--dalamud-dev-plugin-directory='{startInfo.DefaultPluginDirectory}'",
            $"--dalamud-asset-directory='{startInfo.AssetDirectory}'",
            $"--dalamud-client-language={(int)startInfo.Language}",
            $"--dalamud-delay-initialize={startInfo.DelayInitializeMs}"
            };

        if (loadMethod == DalamudLoadMethod.ACLonly)
            launchArguments.Add("--without-dalamud");

        if (fakeLogin)
            launchArguments.Add("--fake-arguments");

        launchArguments.Add("--");
        launchArguments.Add(gameArgs);

        var psi = new ProcessStartInfo(runner.FullName);
        psi.Arguments = string.Join(" ", launchArguments);

        foreach (var keyValuePair in environment)
        {
            if (psi.EnvironmentVariables.ContainsKey(keyValuePair.Key))
                psi.EnvironmentVariables[keyValuePair.Key] = keyValuePair.Value;
            else
                psi.EnvironmentVariables.Add(keyValuePair.Key, keyValuePair.Value);
        }

        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;

        var dalamudProcess = Process.Start(psi);
        var output = dalamudProcess.StandardOutput.ReadLine();

        if (output == null)
            throw new DalamudRunnerException("An internal Dalamud error has occured");

        try
        {
            var dalamudConsoleOutput = JsonConvert.DeserializeObject<DalamudConsoleOutput>(output);
            var gameProcess = Process.GetProcessById(dalamudConsoleOutput.pid);

            if (gameProcess.Handle != (IntPtr)dalamudConsoleOutput.handle)
                Log.Warning($"Internal process handle [{(long)gameProcess.Handle}] does not match Dalamud provided one [{dalamudConsoleOutput.handle}]");

            return gameProcess;
        }
        catch (JsonReaderException ex)
        {
            Log.Error(ex, $"Couldn't parse Dalamud output: {output}");
            return null;
        }
    }
}