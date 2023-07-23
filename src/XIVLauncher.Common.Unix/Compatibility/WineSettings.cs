using System.Collections.Generic;
using System.IO;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    private string RunCommandFolder;

    public string RunCommand
    {
        get
        {
            if (File.Exists(Path.Combine(RunCommandFolder, "wine64")))
                return Path.Combine(RunCommandFolder, "wine64");

            if (File.Exists(Path.Combine(RunCommandFolder, "wine")))
                return Path.Combine(RunCommandFolder, "wine");
                
            return string.Empty;
        }
    }

    public string WineServer { get; }

    public string Folder { get; }

    public string DownloadUrl { get; }

    public bool IsManaged { get; }

    public Dictionary<string, string> Environment { get; }

    public WineSettings(string customWinePath, string managedFolder, string managedDownloadUrl, string rootFolder, Dictionary<string, string> env = null)
    {
        Folder = managedFolder;
        DownloadUrl = managedDownloadUrl;
        Environment = env ?? new Dictionary<string, string>();

        // Use customWinePath to pass in the custom wine bin/ path. If it's empty, we construct the RunCommand from the managedFolder.
        if (string.IsNullOrEmpty(customWinePath))
        {
            var wineBinPath = Path.Combine(Path.Combine(rootFolder, "compatibilitytool", "wine"), managedFolder, "bin");
            RunCommandFolder = wineBinPath;
            WineServer = Path.Combine(wineBinPath, "wineserver");
            IsManaged = true;
        }
        else
        {
            RunCommandFolder = customWinePath;
            WineServer = Path.Combine(customWinePath, "wineserver");
            IsManaged = false;
        }
    }
}