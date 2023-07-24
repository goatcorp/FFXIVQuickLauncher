using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public bool Enabled { get; }

    public string Folder { get; }

    public string DownloadUrl { get; }

    public Dictionary<string, string> Environment { get; }

    private const string ALLOWED_CHARS = "^[0-9a-zA-Z,=.]+$";

    private const string ALLOWED_WORDS = "^(?:devinfo|fps|frametimes|submissions|drawcalls|pipelines|descriptors|memory|gpuload|version|api|cs|compiler|samplers|scale=(?:[0-9])*(?:.(?:[0-9])+)?)$";

    public DxvkSettings(string folder, string url, string storageFolder, bool? async, int maxFrameRate, bool enabled = true)
    {
        Folder = folder;
        DownloadUrl = url;
        Enabled = enabled;

        var dxvkConfigPath = new DirectoryInfo(Path.Combine(storageFolder, "compatibilitytool", "dxvk"));
        Environment = new Dictionary<string, string>
        {
            { "DXVK_LOG_PATH", Path.Combine(storageFolder, "logs") },
            { "DXVK_CONFIG_FILE", Path.Combine(dxvkConfigPath.FullName, "dxvk.conf") },
            { "DXVK_FRAME_RATE", (maxFrameRate).ToString() }
        };
        if (async is not null)
            Environment.Add("DXVK_ASYNC", async.Value ? "1" : "0");
        var dxvkCachePath = new DirectoryInfo(Path.Combine(dxvkConfigPath.FullName, "cache"));
        if (!dxvkCachePath.Exists) dxvkCachePath.Create();
        Environment.Add("DXVK_STATE_CACHE_PATH", Path.Combine(dxvkCachePath.FullName, folder));
    }

    public static bool CheckDxvkHudString(string customHud)
    {
        if (string.IsNullOrWhiteSpace(customHud)) return false;
        if (customHud == "1") return true;
        if (!Regex.IsMatch(customHud,ALLOWED_CHARS)) return false;

        string[] hudvars = customHud.Split(",");

        return hudvars.All(hudvar => Regex.IsMatch(hudvar, ALLOWED_WORDS));        
    }
}