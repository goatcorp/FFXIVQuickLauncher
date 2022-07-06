using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace XIVLauncher.Common.Util;

public static class GameHelpers
{
    /// <summary>
    ///     Returns <see langword="true"/> if the current system region is set to North America.
    /// </summary>
    public static bool IsRegionNorthAmerica()
    {
        return RegionInfo.CurrentRegion.TwoLetterISORegionName is "US" or "MX" or "CA";
    }

    public static bool IsValidGamePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return Directory.Exists(Path.Combine(path, "game")) && Directory.Exists(Path.Combine(path, "boot"));
    }

    public static bool CanMightNotBeInternationalClient(string path) 
    {
        if (Directory.Exists(Path.Combine(path, "sdo")))
            return true;

        if (File.Exists(Path.Combine(path, "boot", "FFXIV_Boot.exe")))
            return true;

        return false;
    }

    public static bool LetChoosePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        var di = new DirectoryInfo(path);

        if (di.Name == "game")
            return false;

        if (di.Name == "boot")
            return false;

        if (di.Name == "sqpack")
            return false;

        return true;
    }

    public static FileInfo GetOfficialLauncherPath(DirectoryInfo gamePath) => new(Path.Combine(gamePath.FullName, "boot", "ffxivboot.exe"));

    public static void StartOfficialLauncher(DirectoryInfo gamePath, bool isSteam, bool isFreeTrial)
    {
        var args = string.Empty;

        if (isSteam && isFreeTrial)
        {
            args = "-issteamfreetrial";
        }
        else if (isSteam)
        {
            args = "-issteam";
        }

        Process.Start(GetOfficialLauncherPath(gamePath).FullName, args);
    }

    public static bool CheckIsGameOpen()
    {
#if DEBUG
        return false;
#endif

        var procs = Process.GetProcesses();

        if (procs.Any(x => x.ProcessName == "ffxiv"))
            return true;

        if (procs.Any(x => x.ProcessName == "ffxiv_dx11"))
            return true;

        if (procs.Any(x => x.ProcessName == "ffxivboot"))
            return true;

        if (procs.Any(x => x.ProcessName == "ffxivlauncher"))
            return true;

        return false;
    }

    public static string ToMangledSeBase64(byte[] input)
    {
        return Convert.ToBase64String(input)
                      .Replace('+', '-')
                      .Replace('/', '_')
                      .Replace('=', '*');
    }
}