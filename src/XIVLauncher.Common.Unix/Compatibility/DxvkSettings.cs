using System.Collections.Generic;
using Serilog;

namespace XIVLauncher.Common.Unix.Compatibility;

public class DxvkSettings
{
    public string Folder { get; }

    public string DownloadUrl { get; }

    public Dictionary<string, string> Environment { get; }

    public bool Enabled { get; }

    public DxvkSettings(string folder, string url, string rootFolder, Dictionary<string, string> env = null, bool enabled = true)
    {
        Folder = folder;
        DownloadUrl = url;
        Environment = env ?? new Dictionary<string, string>();
        Enabled = enabled;
    }
}