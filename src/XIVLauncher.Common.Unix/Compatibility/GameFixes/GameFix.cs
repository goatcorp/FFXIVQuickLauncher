using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes;

public abstract class GameFix(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory, DirectoryInfo tempDirectory)
{
    public abstract string LoadingTitle { get; }

    public GameFixApply.UpdateProgressDelegate? UpdateProgress;

    public DirectoryInfo WinePrefixDir { get; private set; } = winePrefixDirectory;

    public DirectoryInfo ConfigDir { get; private set; } = configDirectory;

    public DirectoryInfo GameDir { get; private set; } = gameDirectory;

    public DirectoryInfo TempDir { get; private set; } = tempDirectory;

    public abstract void Apply();
}
