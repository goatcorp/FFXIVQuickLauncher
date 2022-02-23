using System.Collections.Generic;
using System.Diagnostics;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface IRunner
{
    Process Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, bool runas);
}