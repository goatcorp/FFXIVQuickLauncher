using System.IO;
using XIVLauncher.Common.Unix.Compatibility.GameFixes.Implementations;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes;

public class GameFixApply
{
    private readonly GameFix[] fixes;

    public delegate void UpdateProgressDelegate(string loadingText, bool hasProgress, float progress);

    public event UpdateProgressDelegate UpdateProgress;

    public GameFixApply(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory, DirectoryInfo tempDirectory)
    {
        this.fixes = new GameFix[]
        {
            new MacVideoFix(gameDirectory, configDirectory, winePrefixDirectory, tempDirectory),
        };
    }

    public void Run()
    {
        foreach (GameFix fix in this.fixes)
        {
            this.UpdateProgress?.Invoke(fix.LoadingTitle, false, 0f);

            fix.UpdateProgress += this.UpdateProgress;
            fix.Apply();
            fix.UpdateProgress -= this.UpdateProgress;
        }
    }
}