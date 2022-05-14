using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes;

public abstract class GameFix
{
    public GameFix(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory)
    {
        GameDir = gameDirectory;
        ConfigDir = configDirectory;
        WinePrefixDir = winePrefixDirectory;
    }

    public abstract string LoadingTitle { get; }

    public GameFixApply.UpdateProgressDelegate UpdateProgress;

    public DirectoryInfo WinePrefixDir { get; private set; }

    public DirectoryInfo ConfigDir { get; private set; }

    public DirectoryInfo GameDir { get; private set; }

    public abstract void Apply();
}