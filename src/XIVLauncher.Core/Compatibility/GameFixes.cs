namespace XIVLauncher.Core.Compatibility;

public static class GameFixes
{
    public static void AddDefaultConfig(DirectoryInfo prefix)
    {
        var usersDir = new DirectoryInfo(Path.Combine(prefix.FullName, "drive_c", "users"));
        var thisUser = usersDir.GetDirectories().First(x => x.Name != "Public");

        var myGames = new DirectoryInfo(Path.Combine(thisUser.FullName, "Documents", "My Games"));
        if (!myGames.Exists)
            myGames.Create();

        var ffxiv = new DirectoryInfo(Path.Combine(myGames.FullName, "FINAL FANTASY XIV - A Realm Reborn"));
        if (!ffxiv.Exists)
            ffxiv.Create();

        var gameConf = Path.Combine(ffxiv.FullName, "FFXIV.cfg");
        if (!File.Exists(gameConf))
            File.WriteAllText(gameConf, "<FINAL FANTASY XIV Config File>\n\n<Cutscene Settings>\nCutsceneMovieOpening 1");

        var bootConf = Path.Combine(ffxiv.FullName, "FFXIV_BOOT.cfg");
        if (!File.Exists(bootConf))
            File.WriteAllText(bootConf, "<FINAL FANTASY XIV Boot Config File>\n\n<Version>\nBrowser 1\nStartupCompleted 1");
    }
}
