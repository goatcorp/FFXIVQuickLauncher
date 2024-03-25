using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Serilog;
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
            return this.dalamudLauncher.Run(new FileInfo(path), arguments, environment);
        }
        else
        {
            return compatibility.RunInPrefix($"\"{path}\" {arguments}", workingDirectory, environment, writeLog: true);
        }
    }

    private void ProcessOutputHandler(object sender, DataReceivedEventArgs args)
    {
        Log.Information("Process output: {0}", args.Data);
    }
    
    public Process? Run(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, bool withCompatibility)
    {
        if (withCompatibility)
        {
            return compatibility.RunInPrefix($"\"{path}\" {arguments}", workingDirectory, environment, writeLog: true);
        }
        
        var psi = new ProcessStartInfo(path, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        foreach (var envVar in environment)
        {
            if (psi.Environment.ContainsKey(envVar.Key))
            {
                psi.Environment[envVar.Key] = envVar.Value;
            }
            else
            {
                psi.Environment.Add(envVar.Key, envVar.Value);
            }
        }

        var p = new Process()
        {
            StartInfo = psi,
        };

        p.OutputDataReceived += ProcessOutputHandler;
        
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        return p;
    }
}