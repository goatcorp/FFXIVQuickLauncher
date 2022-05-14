using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes.Implementations;

public class DefaultConfigGameFix : GameFix
{
    public DefaultConfigGameFix(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory, DirectoryInfo tempDirectory)
        : base(gameDirectory, configDirectory, winePrefixDirectory, tempDirectory)
    {
    }

    public override string LoadingTitle => "Setting up default configuration...";

    public override void Apply()
    {
        if (!ConfigDir.Exists)
            ConfigDir.Create();

        var gameConf = Path.Combine(ConfigDir.FullName, "FFXIV.cfg");
        if (!File.Exists(gameConf))
            File.WriteAllText(gameConf, "<FINAL FANTASY XIV Config File>\n\n<Cutscene Settings>\nCutsceneMovieOpening 1");

        var bootConf = Path.Combine(ConfigDir.FullName, "FFXIV_BOOT.cfg");
        if (!File.Exists(bootConf))
            File.WriteAllText(bootConf, "<FINAL FANTASY XIV Boot Config File>\n\n<Version>\nBrowser 1\nStartupCompleted 1");
    }
}