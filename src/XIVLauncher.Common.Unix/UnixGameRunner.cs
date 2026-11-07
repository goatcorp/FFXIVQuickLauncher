using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixGameRunner : IGameRunner
{
    private readonly CompatibilityTools compatibility;
    private readonly DalamudLauncher dalamudLauncher;
    private readonly bool dalamudOk;

    public UnixGameRunner(CompatibilityTools compatibility, DalamudLauncher dalamudLauncher, bool dalamudOk)
    {
        this.compatibility = compatibility;
        this.dalamudLauncher = dalamudLauncher;
        this.dalamudOk = dalamudOk;
    }

    public Process? Start(string path, string workingDirectory, string arguments, IDictionary<string, string> environment, DpiAwareness dpiAwareness, string additionalInjectorArgs)
    {
        if (dalamudOk)
        {
            return this.dalamudLauncher.Run(new FileInfo(path), arguments, environment, additionalInjectorArgs);
        }
        else
        {
            return compatibility.RunInPrefix($"\"{path}\" {arguments}", workingDirectory, environment, writeLog: true);
        }
    }

    private const int CLOCK_MONOTONIC_RAW = 4;
    private const long MsPerSecond = 1000;
    private const long NsPerMs = 1_000_000;

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long tv_sec;
        public long tv_nsec;
    }

    [DllImport("c")]
    private static extern int clock_gettime(int clock_id, out Timespec ts);

    [DllImport("c")]
    private static extern ulong clock_gettime_nsec_np(int clock_id);

    // Must match the clock Wine uses for the game's GetTickCount().
    public uint GetTickCount()
    {
        if (OperatingSystem.IsMacOS())
        {
            return (uint)(clock_gettime_nsec_np(CLOCK_MONOTONIC_RAW) / NsPerMs);
        }

        clock_gettime(CLOCK_MONOTONIC_RAW, out var ts);
        return (uint)((ts.tv_sec * MsPerSecond) + (ts.tv_nsec / NsPerMs));
    }
}
