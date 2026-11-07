using System.Collections.Generic;
using System.Diagnostics;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IGameRunner
{
    Process? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness, string additionalInjectorArgs);

    // Tick count in the same context as the game GetTickCount() calls
    uint GetTickCount();
}
