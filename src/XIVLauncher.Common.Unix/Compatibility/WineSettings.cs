using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public enum WineStartupType
{
    [SettingsDescription("Managed by XIVLauncher", "The game installation and wine setup is managed by XIVLauncher - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Proton (requires Steam)", "Use a Proton installation, which includes DXVK and other features. Experimental.")]
    Proton,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public class WineSettings
{
    public WineStartupType StartupType { get; private set; }
    public string CustomBinPath { get; private set; }

    public ProtonSettings Proton { get; private set; }

    public bool EsyncOn { get; private set; }
    public bool FsyncOn { get; private set; }

    public string DebugVars { get; private set; }
    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public WineSettings(WineStartupType? startupType, string customBinPath, ProtonSettings protonSettings, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
    {
        this.StartupType = startupType ?? WineStartupType.Custom;
        this.CustomBinPath = customBinPath;
        this.Proton = protonSettings;
        this.EsyncOn = esyncOn ?? false;
        this.FsyncOn = fsyncOn ?? false;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }

    public WineSettings(WineStartupType? startupType, string customBinPath, string debugVars, FileInfo logFile, DirectoryInfo prefix, bool? esyncOn, bool? fsyncOn)
        : this(startupType, customBinPath, new ProtonSettings(new DirectoryInfo(Path.Combine(prefix.Parent.FullName,"protonprefix")),
            Path.Combine(System.Environment.GetEnvironmentVariable("HOME"),".steam","root"),
            Path.Combine(System.Environment.GetEnvironmentVariable("HOME"),".steam","root","steamapps","common","Proton 7.0")),
            debugVars, logFile, prefix, esyncOn, fsyncOn)
    {    }
}