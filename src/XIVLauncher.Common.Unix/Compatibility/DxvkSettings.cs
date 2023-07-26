using System.IO;
using System.Collections.Generic;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public bool Enabled { get; }

    public string FolderName { get; }

    public string DownloadUrl { get; }

    public Dictionary<string, string> Environment { get; }

    public DxvkSettings(string folder, string url, string storageFolder, bool async, int maxFrameRate, bool dxvkHudEnabled, string dxvkHudString, bool mangoHudEnabled, bool mangoHudCustomIsFile, string customMangoHud, bool enabled = true)
    {
        FolderName = folder;
        DownloadUrl = url;
        Enabled = enabled;

        var dxvkConfigPath = new DirectoryInfo(Path.Combine(storageFolder, "compatibilitytool", "dxvk"));
        Environment = new Dictionary<string, string>
        {
            { "DXVK_LOG_PATH", Path.Combine(storageFolder, "logs") },
            { "DXVK_CONFIG_FILE", Path.Combine(dxvkConfigPath.FullName, "dxvk.conf") },
        };
        
        if (maxFrameRate != 0)
            Environment.Add("DXVK_FRAME_RATE", (maxFrameRate).ToString());
        
        if (async)
            Environment.Add("DXVK_ASYNC", "1");
        
        var dxvkCachePath = new DirectoryInfo(Path.Combine(dxvkConfigPath.FullName, "cache"));
        if (!dxvkCachePath.Exists) dxvkCachePath.Create();
        Environment.Add("DXVK_STATE_CACHE_PATH", Path.Combine(dxvkCachePath.FullName, folder));

        if (dxvkHudEnabled)
            Environment.Add("DXVK_HUD", UnixHelpers.DxvkHudStringIsValid(dxvkHudString) ? dxvkHudString : "1");

        if (mangoHudEnabled && UnixHelpers.MangoHudIsInstalled())
        {
            Environment.Add("MANGOHUD", "1");
            if (mangoHudCustomIsFile)
            {
                if (File.Exists(customMangoHud))
                    Environment.Add("MANGOHUD_CONFIGFILE", customMangoHud);
                else
                    Environment.Add("MANGOHUD_CONFIG", "");
            }
            else
            {
                Environment.Add("MANGOHUD_CONFIG", customMangoHud);
            }
        }
    }
}