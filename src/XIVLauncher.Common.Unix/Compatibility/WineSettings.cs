using System;
using System.IO;
using System.Collections;

namespace XIVLauncher.Common.Unix.Compatibility;

public enum WineStartupType
{
    [SettingsDescription("Proton (requires Steam)", "Use a Proton installation, which includes DXVK and other features built in.")]
    Proton,

    [SettingsDescription("Managed by XIVLauncher", "The game installation and wine setup is managed by XIVLauncher - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Official Wine-XIV 7.10 (Current Default)", "A custom version of Wine-TKG 7.10 with XIV patches.")]
    Official7_10,

    [SettingsDescription("Official Wine-XIV 7.15 (Untested)", "A custom version of Wine-TKG 7.15 with XIV patches.")]
    Official7_15, 

    [SettingsDescription("Unofficial Wine Proton7-35", "Based on Wine-GE, but with XIV patches applied.")]
    Unoffical7_35,

    [SettingsDescription("Unofficial Wine Proton7-36", "Based on Wine-GE, but with XIV and Haptic Feedback patches applied.")]
    Unoffical7_36,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public class WineSettings
{
    public WineStartupType StartupType { get; private set; }
    public string CustomBinPath { get; private set; }

    public string SteamPath { get; private set; }
    public string ProtonPath { get; private set; }

    public bool EsyncOn { get; private set; }
    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }
    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }
    public DirectoryInfo ProtonPrefix { get; private set; }

    public string WineFolder { get; private set; }

    public string WineURL { get; private set; }

#if WINE_XIV_ARCH_LINUX
    private const string DISTRO = "arch";
#elif WINE_XIV_FEDORA_LINUX
    private const string DISTRO = "fedora";
#else
    private const string DISTRO = "ubuntu";
#endif

    public WineSettings(WineStartupType? startupType, string customBinPath, string steamPath, string protonPath, string debugVars,
                        FileInfo logFile, DirectoryInfo prefix, DirectoryInfo protonPrefix, bool? esyncOn, bool? fsyncOn)
    {
        this.StartupType = startupType ?? WineStartupType.Custom;
        this.CustomBinPath = customBinPath;
        this.SteamPath = steamPath;
        this.ProtonPath = protonPath;
        this.EsyncOn = esyncOn ?? false;
        this.FsyncOn = fsyncOn ?? false;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
        this.ProtonPrefix = protonPrefix;

        switch (StartupType)
        {
            case WineStartupType.Managed:
            case WineStartupType.Official7_10:
                WineURL = $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.10.r3.g560db77d/wine-xiv-staging-fsync-git-{DISTRO}-7.10.r3.g560db77d.tar.xz";
                WineFolder = "wine-xiv-staging-fsync-git-7.10.r3.g560db77d";
                break;
            
            case WineStartupType.Official7_15:
                WineURL = $"https://github.com/goatcorp/wine-xiv-git/releases/download/7.15.r4.gfa8d0abc/wine-xiv-staging-fsync-git-{DISTRO}-7.15.r4.gfa8d0abc.tar.xz";
                WineFolder = "wine-xiv-staging-fsync-git-7.15.r4.gfa8d0abc";
                break;
            
            case WineStartupType.Unoffical7_35:
                WineURL = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton7-35/unofficial-wine-xiv-Proton7-35-x86_64.tar.xz";
                WineFolder = "unofficial-wine-xiv-Proton7-35-x86_64";
                break;
            
            case WineStartupType.Unoffical7_36:
                WineURL = "https://github.com/rankynbass/wine-ge-xiv/releases/download/xiv-Proton7-36/unofficial-wine-xiv-Proton7-36-x86_64.tar.xz";
                WineFolder = "unofficial-wine-xiv-Proton7-36-x86_64";
                break;

            case WineStartupType.Proton:
            case WineStartupType.Custom:
                WineURL = "";
                WineFolder = "";
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public WineSettings(WineStartupType? startupType, string customBinPath, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
        : this(startupType, customBinPath, Path.Combine(System.Environment.GetEnvironmentVariable("HOME"),".steam","root"),
            Path.Combine(System.Environment.GetEnvironmentVariable("HOME"),".steam","root","steamapps","common","Proton 7.0"),
            debugVars, logFile, prefix, new DirectoryInfo(Path.Combine(prefix.Parent.FullName,"protonprefix")), esyncOn, fsyncOn)
    {    }
}