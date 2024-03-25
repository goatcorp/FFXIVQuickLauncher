using System.Collections.Generic;
using System.Diagnostics;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IGameRunner
{
    Process? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness);
    
    Process? Run(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, bool withCompatibility);
}