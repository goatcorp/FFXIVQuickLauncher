using System.Collections.Generic;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IGameRunner
{
    object? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness);
}