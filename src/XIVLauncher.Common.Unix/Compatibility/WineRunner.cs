using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineRunner
{
    public string RunCommand { get; }

    public string RunArguments { get; }

    public string WineServer { get; }

    public string PathArguments { get; }

    private string wineFolder { get; }

    private string downloadUrl;

    private string toolFolder;

    private string xlcoreFolder;

    public bool IsProton { get; }

    public Dictionary<string, string> Environment { get; }

    public WineRunner(string runCmd, string runArgs, string folder, string url, string rootFolder, Dictionary<string, string> env = null, bool is64only = false, bool isProton = false)
    {
        RunArguments = (isProton) ? "runinprefix" : "";
        wineFolder = folder;
        downloadUrl = url;
        toolFolder = Path.Combine(rootFolder, "compatibilitytool", "beta");
        xlcoreFolder = rootFolder;
        Environment = (env is null) ? new Dictionary<string, string>() : env;
        IsProton = isProton;
        if (string.IsNullOrEmpty(runCmd))
        {
            RunCommand = Path.Combine(toolFolder, folder, "bin", (is64only) ? "wine" : "wine64");
            WineServer = Path.Combine(toolFolder, folder, "bin", "wineserver");
        }
        else
        {
            if (isProton)
            {
                RunCommand = Path.Combine(runCmd, "proton");
                WineServer = Path.Combine(runCmd, "files", "bin", "wineserver");
            }
            else
            {
                if (File.Exists(Path.Combine(runCmd, "wine64")))
                    RunCommand = Path.Combine(runCmd, "wine64");
                else if (File.Exists(Path.Combine(runCmd, "wine")))
                    RunCommand = Path.Combine(runCmd, "wine");
                else
                    throw new FileNotFoundException($"There is no wine or wine64 binary at {runCmd}.");
                WineServer = Path.Combine(runCmd, "wineserver");
            }
        }
    }

    public async Task Install()
    {
        if (CompatibilityTools.IsDirectoryEmpty(Path.Combine(toolFolder, wineFolder)))
        {
            Log.Information($"Downloading Tool to {toolFolder}");
            if (string.IsNullOrEmpty(downloadUrl))
            {
                Log.Error($"Attempted to download Wine runner without a download URL.");
                throw new InvalidOperationException($"Wine runner does not exist, and no download URL was provided for it.");
            }
            Log.Information($"{wineFolder} does not exist. Downloading...");
            using var client = new HttpClient();
            var tempPath = Path.GetTempFileName();

            File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
            PlatformHelpers.Untar(tempPath, toolFolder);

            File.Delete(tempPath);
        }
        else
            Log.Information("Did not try to download Wine.");
    }

    public string GetWinePrefix()
    {
        if (IsProton)
            return Path.Combine(xlcoreFolder, "protonprefix", "pfx");
        return Path.Combine(xlcoreFolder, "wineprefix");
    }
}