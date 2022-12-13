#nullable enable
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public bool Enabled { get; }

    public string DownloadURL { get; }

    public string FolderName { get; }

    public Dictionary<string, string> DxvkVars { get; }

    public Dxvk.DxvkHudType DxvkHud { get; }

    public Dxvk.DxvkVersion DxvkVersion { get; }

    private const string ALLOWED_CHARS = "^[0-9a-zA-Z,=.]+$";

    private const string ALLOWED_WORDS = "^(?:devinfo|fps|frametimes|submissions|drawcalls|pipelines|descriptors|memory|gpuload|version|api|cs|compiler|samplers|scale=(?:[0-9])*(?:.(?:[0-9])+)?)$";

    public DxvkSettings(Dxvk.DxvkHudType hud, DirectoryInfo corePath, Dxvk.DxvkVersion version, bool enabled = true, string? dxvkHudCustom = null, FileInfo? mangoHudConfig = null, bool async = true,
                        int maxFrameRate = 0)
    {
        Enabled = enabled;
        DxvkHud = hud;
        var dxvkConfigPath = new DirectoryInfo(Path.Combine(corePath.FullName, "compatibilitytool", "dxvk"));
        if (!dxvkConfigPath.Exists)
            dxvkConfigPath.Create();
        DxvkVars = new Dictionary<string, string>
        {
            { "DXVK_LOG_PATH", Path.Combine(corePath.FullName, "logs") },
            { "DXVK_CONFIG_FILE", Path.Combine(dxvkConfigPath.FullName, "dxvk.conf") },
            { "DXVK_ASYNC", async ? "1" : "0" },
            { "DXVK_FRAME_RATE", (maxFrameRate).ToString() }
        };
        DxvkVersion = version;
        var release = DxvkVersion switch
        {
            Dxvk.DxvkVersion.v1_10_1 => "1.10.1",
            Dxvk.DxvkVersion.v1_10_2 => "1.10.2",
            Dxvk.DxvkVersion.v1_10_3 => "1.10.3",
            Dxvk.DxvkVersion.v2_0 => "2.0",
            _ => throw new ArgumentOutOfRangeException(),
        };
        DownloadURL = $"https://github.com/Sporif/dxvk-async/releases/download/{release}/dxvk-async-{release}.tar.gz";
        FolderName = $"dxvk-async-{release}";
        DirectoryInfo dxvkCachePath = new DirectoryInfo(Path.Combine(dxvkConfigPath.FullName, "cache"));
        if (!dxvkCachePath.Exists) dxvkCachePath.Create();
        this.DxvkVars.Add("DXVK_STATE_CACHE_PATH", Path.Combine(dxvkCachePath.FullName, release + (async ? "-async" : "")));

        switch(this.DxvkHud)
        {
            case Dxvk.DxvkHudType.Fps:
                DxvkVars.Add("DXVK_HUD","fps");
                DxvkVars.Add("MANGOHUD","0");
                break;

            case Dxvk.DxvkHudType.Custom:
                if (!CheckDxvkHudString(dxvkHudCustom))
                    dxvkHudCustom = "fps,frametimes,gpuload,version";
                DxvkVars.Add("DXVK_HUD", dxvkHudCustom!);
                DxvkVars.Add("MANGOHUD","0");
                break;

            case Dxvk.DxvkHudType.Full:
                DxvkVars.Add("DXVK_HUD","full");
                DxvkVars.Add("MANGOHUD","0");
                break;

            case Dxvk.DxvkHudType.MangoHud:
                DxvkVars.Add("DXVK_HUD","0");
                DxvkVars.Add("MANGOHUD","1");
                DxvkVars.Add("MANGOHUD_CONFIG", "");
                break;

            case Dxvk.DxvkHudType.MangoHudCustom:
                DxvkVars.Add("DXVK_HUD","0");
                DxvkVars.Add("MANGOHUD","1");

                if (mangoHudConfig is null)
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var conf1 = Path.Combine(corePath.FullName, "MangoHud.conf");
                    var conf2 = Path.Combine(home, ".config", "MangoHud", "wine-ffxiv_dx11.conf");
                    var conf3 = Path.Combine(home, ".config", "MangoHud", "MangoHud.conf");
                    if (File.Exists(conf1))
                        mangoHudConfig = new FileInfo(conf1);
                    else if (File.Exists(conf2))
                        mangoHudConfig = new FileInfo(conf2);
                    else if (File.Exists(conf3))
                        mangoHudConfig = new FileInfo(conf3);
                }

                if (mangoHudConfig != null && mangoHudConfig.Exists)
                    DxvkVars.Add("MANGOHUD_CONFIGFILE", mangoHudConfig.FullName);
                else
                    DxvkVars.Add("MANGOHUD_CONFIG", "");
                break;

            case Dxvk.DxvkHudType.MangoHudFull:
                DxvkVars.Add("DXVK_HUD","0");
                DxvkVars.Add("MANGOHUD","1");
                DxvkVars.Add("MANGOHUD_CONFIG","full");
                break;

            case Dxvk.DxvkHudType.None:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static bool CheckDxvkHudString(string? customHud)
    {
        if (string.IsNullOrWhiteSpace(customHud)) return false;
        if (customHud == "1") return true;
        if (!Regex.IsMatch(customHud,ALLOWED_CHARS)) return false;

        string[] hudvars = customHud.Split(",");

        return hudvars.All(hudvar => Regex.IsMatch(hudvar, ALLOWED_WORDS));
    }
}
