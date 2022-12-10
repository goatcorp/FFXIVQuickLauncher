using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public string DownloadURL { get; private set; }

    public string FolderName { get; private set; }

    public Dictionary<string, string> DxvkVars { get; private set; }

    public Dxvk.DxvkHudType DxvkHud { get; private set; }

    public Dxvk.DxvkVersion DxvkVersion { get; private set; }

    private const string ALLOWED_CHARS = "^[0-9a-zA-Z,=.]+$";
    private const string ALLOWED_WORDS = "^(?:devinfo|fps|frametimes|submissions|drawcalls|pipelines|descriptors|memory|gpuload|version|api|cs|compiler|samplers|scale=(?:[0-9])*(?:.(?:[0-9])+)?)$";

    public DxvkSettings(Dxvk.DxvkHudType hud = Dxvk.DxvkHudType.None, string dxvkHudCustom="",
        string mangoHudPath="", bool? async = true, int? frameRate = 0,
        Dxvk.DxvkVersion version = Dxvk.DxvkVersion.v1_10_1, DirectoryInfo? corePath = null)
    {
        this.DxvkHud = hud;
        string home = Environment.GetEnvironmentVariable("HOME");
        corePath ??= new DirectoryInfo(Path.Combine(home,".xlcore"));
        var dxvkConfigPath = new DirectoryInfo(Path.Combine(corePath.FullName,".dxvk"));
        if (!dxvkConfigPath.Exists)
            dxvkConfigPath.Create();
        this.DxvkVars = new Dictionary<string, string> ();
        this.DxvkVars.Add("DXVK_LOG_PATH",Path.Combine(corePath.FullName,"logs"));
        this.DxvkVars.Add("DXVK_CONFIG_FILE",Path.Combine(dxvkConfigPath.FullName,"dxvk.conf"));
        this.DxvkVars.Add("DXVK_ASYNC", ((async ?? false) ? "1" : "0"));
        frameRate ??= 0;
        if (frameRate > 0) this.DxvkVars.Add("DXVK_FRAME_RATE", (frameRate).ToString());
        this.DxvkVersion = version;
        string release = this.DxvkVersion switch
        {
            Dxvk.DxvkVersion.v1_10_1 => "1.10.1",
            Dxvk.DxvkVersion.v1_10_2 => "1.10.2",
            Dxvk.DxvkVersion.v1_10_3 => "1.10.3",
            Dxvk.DxvkVersion.v2_0 => "2.0",
            _ => throw new ArgumentOutOfRangeException(),
        };
        this.DownloadURL = $"https://github.com/Sporif/dxvk-async/releases/download/{release}/dxvk-async-{release}.tar.gz";
        this.FolderName = $"dxvk-async-{release}";
        string dxvkCachePath = Path.Combine(dxvkConfigPath.FullName, release + ((async ?? false) ? "-async" : "")); 
        this.DxvkVars.Add("DXVK_STATE_CACHE_PATH", dxvkCachePath);
        switch(this.DxvkHud)
        {
            case Dxvk.DxvkHudType.Fps:
                DxvkVars.Add("DXVK_HUD","fps");
                DxvkVars.Add("MANGOHUD","0");
                break;
            case Dxvk.DxvkHudType.Custom:
                if (!CheckDxvkHudString(dxvkHudCustom)) dxvkHudCustom = "fps,frametimes,gpuload,version";
                DxvkVars.Add("DXVK_HUD",dxvkHudCustom);
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
                if (mangoHudPath == "")
                {
                    string conf1 = Path.Combine(corePath.FullName,"MangoHud.conf");
                    string conf2 = Path.Combine(home,".config","MangoHud","wine-ffxiv_dx11.conf");
                    string conf3 = Path.Combine(home,".config","MangoHud","MangoHud.conf");
                    if (CheckMangoHudPath(conf1))
                        mangoHudPath = conf1;
                    else if (CheckMangoHudPath(conf2))
                        mangoHudPath = conf2;
                    else if (CheckMangoHudPath(conf3))
                        mangoHudPath = conf3;
                }
                if (CheckMangoHudPath(mangoHudPath))
                    DxvkVars.Add("MANGOHUD_CONFIGFILE",mangoHudPath);
                else
                    DxvkVars.Add("MANGOHUD_CONFIG","");
                break;
            case Dxvk.DxvkHudType.MangoHudFull:
                DxvkVars.Add("DXVK_HUD","0");
                DxvkVars.Add("MANGOHUD","1");
                DxvkVars.Add("MANGOHUD_CONFIG","full");
                break;
            // If DxvkHudType is None, or undefined, don't set anything.
            default:
                break;
        }
    }

    public static bool CheckDxvkHudString(string customHud)
    {
        if (customHud == "1") return true;
        if (string.IsNullOrWhiteSpace(customHud)) return false;
        if (!Regex.IsMatch(customHud,ALLOWED_CHARS)) return false;
        string[] hudvars = customHud.Split(",");
        foreach (var hudvar in hudvars)
        {
            if (!Regex.IsMatch(hudvar,ALLOWED_WORDS))
                return false;
        }
        return true;
    }

    public static bool CheckMangoHudPath(string mangoPath)
    {
        return (File.Exists(mangoPath)) ? true : false;
    }
}
