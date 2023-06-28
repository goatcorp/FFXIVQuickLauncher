using System.Collections.Generic;
using System.IO;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class WineSettings
{
    public string RunCommand { get; private set; }

    public string WineServer { get; }

    public string Folder { get; }

    public string DownloadUrl { get; }

    public Dictionary<string, string> Environment { get; }

    public WineSettings(string customwine, string folder, string url, string rootFolder, Dictionary<string, string> env = null)
    {
        RunCommand = string.Empty;
        Folder = folder;
        DownloadUrl = url;
        Environment = (env is null) ? new Dictionary<string, string>() : env;

        // Use customwine to pass in the custom wine bin/ path. If it's empty, we construct the RunCommand from the folder.
        if (string.IsNullOrEmpty(customwine))
        {
            var wineBinPath = Path.Combine(Path.Combine(rootFolder, "compatibilitytool", "wine"), folder, "bin");
            SetWineOrWine64(wineBinPath);
            WineServer = Path.Combine(wineBinPath, "wineserver");
        }
        else
        {
            SetWineOrWine64(customwine);
            WineServer = Path.Combine(customwine, "wineserver");
        }
    }

    public void SetWineOrWine64(string path)
    {
        if (File.Exists(Path.Combine(path, "wine64")))
            RunCommand = Path.Combine(path, "wine64");
        if (File.Exists(Path.Combine(path, "wine")))
            RunCommand = Path.Combine(path, "wine");
    }
}