// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Diagnostics;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsRunner : IRunner
{
    public Process Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, bool runas)
    {
        throw new System.NotImplementedException();
    }
}
