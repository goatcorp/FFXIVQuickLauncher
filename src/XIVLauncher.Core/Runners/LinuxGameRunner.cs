using System.Diagnostics;
using XIVLauncher.Common;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Core.Runners;

public class LinuxGameRunner : IGameRunner
{
    public Process Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness)
    {
        throw new NotImplementedException();
    }
}