using System.Collections.Generic;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IGameRunner
{
    int? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness);
}