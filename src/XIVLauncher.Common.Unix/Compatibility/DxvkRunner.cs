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

public class DxvkRunner
{
    public string Folder { get; }

    private string downloadUrl;

    private string toolFolder;

    private string prefix;

    public Dictionary<string, string> Environment { get; }

    public bool IsDxvk { get; }

    public DxvkRunner(string folder, string url, string rootFolder, Dictionary<string, string> env = null, bool isDxvk = true)
    {
        Folder = folder;
        downloadUrl = url;
        toolFolder = Path.Combine(rootFolder, "compatibilitytool", "dxvk");
        prefix = Path.Combine(rootFolder, "wineprefix");
        Environment = (env is null) ? new Dictionary<string, string>() : env;
        IsDxvk = isDxvk;
    }

    public async Task Install()
    {
        if (CompatibilityTools.IsDirectoryEmpty(Path.Combine(toolFolder, Folder)))
        {
            if (string.IsNullOrEmpty(downloadUrl))
            {
                Log.Error($"Attempted to download Dxvk without a download URL.");
                throw new InvalidOperationException($"{Folder} does not exist, and no download URL was provided for it.");
            }
            Log.Information($"{Folder} does not exist. Downloading...");
            using var client = new HttpClient();
            var tempPath = Path.GetTempFileName();

            File.WriteAllBytes(tempPath, await client.GetByteArrayAsync(downloadUrl));
            PlatformHelpers.Untar(tempPath, toolFolder);

            File.Delete(tempPath);
        }

        var prefixinstall = Path.Combine(prefix, "drive_c", "windows", "system32");
        var files = new DirectoryInfo(Path.Combine(toolFolder, Folder, "x64")).GetFiles();

        foreach (FileInfo fileName in files)
        {
            fileName.CopyTo(Path.Combine(prefixinstall, fileName.Name), true);
        }
    }
}