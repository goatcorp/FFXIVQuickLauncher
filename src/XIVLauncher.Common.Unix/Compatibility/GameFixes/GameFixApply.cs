using System.IO;
using XIVLauncher.Common.Unix.Compatibility.GameFixes.Implementations;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes;

public class GameFixApply
{
    private readonly GameFix[] fixes;

    public delegate void UpdateProgressDelegate(string loadingText, bool hasProgress, float progress);

    public event UpdateProgressDelegate UpdateProgress;

    public GameFixApply(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory)
    {
        this.fixes = new GameFix[]
        {
            new DefaultConfigGameFix(gameDirectory, configDirectory, winePrefixDirectory),
            new MacVideoFix(gameDirectory, configDirectory, winePrefixDirectory),
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