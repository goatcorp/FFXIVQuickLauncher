using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixDalamudRunner : IDalamudRunner
{
    private readonly CompatibilityTools compatibility;

    public UnixDalamudRunner(CompatibilityTools compatibility)
    {
        this.compatibility = compatibility;
    }

    public void Run(Int32 gameProcessID, FileInfo runner, DalamudStartInfo startInfo, DirectoryInfo gamePath, DalamudLoadMethod loadMethod)
    {
        //Wine want Windows paths here, so we need to fix up the startinfo dirs
        startInfo.WorkingDirectory = compatibility.WineToUnixPath(startInfo.WorkingDirectory);
        startInfo.ConfigurationPath = compatibility.WineToUnixPath(startInfo.ConfigurationPath);
        startInfo.PluginDirectory = compatibility.WineToUnixPath(startInfo.PluginDirectory);
        startInfo.DefaultPluginDirectory = compatibility.WineToUnixPath(startInfo.DefaultPluginDirectory);
        startInfo.AssetDirectory = compatibility.WineToUnixPath(startInfo.AssetDirectory);

        switch (loadMethod)
        {
            case DalamudLoadMethod.EntryPoint:
                throw new NotImplementedException();
                break;

            case DalamudLoadMethod.DllInject:
            {
                var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(startInfo)));
                Dictionary<string, string> environment = new Dictionary<string, string>
                {
                    { "DALAMUD_RUNTIME", compatibility.WineToUnixPath(compatibility.DotnetRuntime.FullName) },
                    { "XL_WINEONLINUX", "true" },
                    { "WINEDEBUG", "-all" }
                };
                compatibility.RunInPrefix($"{runner.FullName} {gameProcessID} {parameters}", environment);
                break;
            }

            default:
                // should not reach
                throw new ArgumentOutOfRangeException();
        }
    }
}