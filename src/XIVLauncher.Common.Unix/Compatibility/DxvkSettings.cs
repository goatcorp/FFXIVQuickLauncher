using System;
using System.Collections.Generic;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public string DownloadURL { get; private set; }

    public string FolderName { get; private set; }

    public Dictionary<string, string> DxvkVars { get; private set; }

    public Dxvk.DxvkHudType DxvkHud { get; private set; }

    public Dxvk.DxvkVersion DxvkVersion { get; private set; }

    public DxvkSettings(Dxvk.DxvkHudType hud = Dxvk.DxvkHudType.None, bool? async = true,
        int? frameRate = 0, Dxvk.DxvkVersion version = Dxvk.DxvkVersion.v1_10_1)
    {
        this.DxvkHud = hud;
        this.DxvkVars = new Dictionary<string, string> ();
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
            _ => VersionOutOfRange(),
        };
        this.DownloadURL = $"https://github.com/Sporif/dxvk-async/releases/download/{release}/dxvk-async-{release}.tar.gz";
        this.FolderName = $"dxvk-async-{release}";

        switch(this.DxvkHud)
        {
            case Dxvk.DxvkHudType.Fps:
                DxvkVars.Add("DXVK_HUD","fps");
                break;
            case Dxvk.DxvkHudType.Full:
                DxvkVars.Add("DXVK_HUD","full");
                break;
            // If DxvkHudType is None, or undefined, don't set anything.
            default:
                break;
        }
    }

    private string VersionOutOfRange() {
        this.DxvkVersion = Dxvk.DxvkVersion.v1_10_1;
        return "1.10.1";
    }
}