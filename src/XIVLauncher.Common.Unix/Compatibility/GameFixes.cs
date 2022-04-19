using System.IO;
using System.Linq;

namespace XIVLauncher.Common.Unix.Compatibility;

public static class GameFixes
{
    public static void AddDefaultConfig(DirectoryInfo configFolder)
    {
        var gameConf = Path.Combine(configFolder.FullName, "FFXIV.cfg");
        if (!File.Exists(gameConf))
            File.WriteAllText(gameConf, "<FINAL FANTASY XIV Config File>\n\n<Cutscene Settings>\nCutsceneMovieOpening 1");

        var bootConf = Path.Combine(configFolder.FullName, "FFXIV_BOOT.cfg");
        if (!File.Exists(bootConf))
            File.WriteAllText(bootConf, "<FINAL FANTASY XIV Boot Config File>\n\n<Version>\nBrowser 1\nStartupCompleted 1");
    }
}
