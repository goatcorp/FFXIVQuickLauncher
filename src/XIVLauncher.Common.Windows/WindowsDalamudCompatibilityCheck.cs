using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Serilog;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows;

public class WindowsDalamudCompatibilityCheck : IDalamudCompatibilityCheck
{
    public void EnsureCompatibility()
    {
        if (!CheckVc2019())
            throw new IDalamudCompatibilityCheck.NoRedistsException();

        EnsureArchitecture();
    }

    private static void EnsureArchitecture()
    {
        var arch = RuntimeInformation.ProcessArchitecture;

        switch (arch)
        {
            case Architecture.X86:
                throw new IDalamudCompatibilityCheck.ArchitectureNotSupportedException("Dalamud is not supported on x86 architecture.");

            case Architecture.X64:
                break;

            case Architecture.Arm:
                throw new IDalamudCompatibilityCheck.ArchitectureNotSupportedException("Dalamud is not supported on ARM32.");

            case Architecture.Arm64:
                throw new IDalamudCompatibilityCheck.ArchitectureNotSupportedException("x64 emulation was not detected. Please make sure to run XIVLauncher with x64 emulation.");
        }
    }

    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    private static bool CheckLibrary(string fileName)
    {
        return LoadLibrary(fileName) != IntPtr.Zero;
    }

    private static bool CheckVc2019()
    {
        // snipped from https://stackoverflow.com/questions/12206314/detect-if-visual-c-redistributable-for-visual-studio-2012-is-installed
        // and https://github.com/bitbeans/RedistributableChecker

        var vc2022Paths = new List<string>
        {
            @"SOFTWARE\Microsoft\DevDiv\VC\Servicing\14.0\RuntimeMinimum",
            @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64",
            @"Installer\Dependencies\Microsoft.VS.VC_RuntimeMinimumVSU_amd64,v14",
            @"Installer\Dependencies\VC,redist.x64,amd64,14.31,bundle",
            @"Installer\Dependencies\VC,redist.x64,amd64,14.30,bundle",
            @"Installer\Dependencies\VC,redist.x64,amd64,14.29,bundle",
            @"Installer\Dependencies\VC,redist.x64,amd64,14.28,bundle"
            // technically, this was introduced in VCrun2017 with 14.16
            // but we shouldn't go that far
        };

        bool passedRegistry = false;

        foreach (var path in vc2022Paths)
        {
            Log.Debug("Checking Registry with: " + path);
            var vcregcheck = Registry.ClassesRoot.OpenSubKey(path, false);
            if (vcregcheck == null) continue;

            var vcVersioncheck = vcregcheck.GetValue("Version") ?? "";

            if (((string)vcVersioncheck).StartsWith("14"))
            {
                passedRegistry = true;
                Log.Debug("Passed Registry Check with: " + path);
                break;
            }
        }

        if (passedRegistry)
        {
            if (!EnvironmentSettings.IsWine)
            {
                if (CheckLibrary("ucrtbase_clr0400") &&
                    CheckLibrary("vcruntime140_clr0400") &&
                    CheckLibrary("vcruntime140"))
                    return true;

                Log.Error("Missing DLL files required by Dalamud.");
            }
            else return true;
        }
        else
        {
            Log.Error("Failed all registry checks to find Visual C++ 2019 Runtime.");
        }

        return false;
    }
}