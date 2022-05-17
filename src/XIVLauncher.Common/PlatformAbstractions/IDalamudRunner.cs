using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using XIVLauncher.Common.Dalamud;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IDalamudRunner
{
    Process? Run(FileInfo runner, bool fakeLogin, DirectoryInfo gamePath, string[] gameArgs, DalamudLoadMethod loadMethod, DalamudStartInfo startInfo, IDictionary<string, string> environment);
}