using System.Diagnostics;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Core.Runners;

public class LinuxDalamudRunner : IDalamudRunner
{
    public void Run(Process gameProcess, FileInfo runner, DalamudStartInfo startInfo, DirectoryInfo gamePath, DalamudLoadMethod loadMethod)
    {
        throw new NotImplementedException();
    }
}