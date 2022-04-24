using System.IO;
using XIVLauncher.Common;

namespace XIVLauncher.Common.Unix.Compatibility;

public enum WineStartupType
{
    [SettingsDescription("Managed by XIVLauncher", "The game installation and wine setup is managed by XIVLauncher - you can leave it up to us.")]
    Managed,

    [SettingsDescription("Custom", "Point XIVLauncher to a custom location containing wine binaries to run the game with.")]
    Custom,
}

public class WineSettings
{
    public WineStartupType StartupType { get; private set; }
    public string CustomBinPath { get; private set; }

    public string DebugVars { get; private set; }
    public FileInfo LogFile { get; private set; }

    public DirectoryInfo Prefix { get; private set; }

    public WineSettings(WineStartupType? startupType, string customBinPath, string debugVars, FileInfo logFile, DirectoryInfo prefix)
    {
        this.StartupType = startupType ?? WineStartupType.Custom;
        this.CustomBinPath = customBinPath;
        this.DebugVars = debugVars;
        this.LogFile = logFile;
        this.Prefix = prefix;
    }
}