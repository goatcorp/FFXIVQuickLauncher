using System;
using System.Diagnostics;
using System.IO;
using XIVLauncher.Common.Dalamud;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IDalamudRunner
{
    void Run(Int32 gameProcessID, FileInfo runner, DalamudStartInfo startInfo, DirectoryInfo gamePath, DalamudLoadMethod loadMethod);
}