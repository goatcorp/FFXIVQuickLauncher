using System;
using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public struct ProtonSettings
{
    public string SteamRoot { get; }

    public string ProtonPath { get; }

    public string SteamLibrary => Path.Combine(SteamRoot, "steamapps", "common");

    public string SoldierRun => Path.Combine(SteamLibrary, "SteamLinuxRuntime_soldier", "run");

    public string SoldierInject => Path.Combine(SteamLibrary, "SteamLinuxRuntime_soldier","_v2-entry-point");

    public string GamePath { get; }

    public string ConfigPath { get; }

    public string CompatMounts => GamePath + ':' + ConfigPath;

    public ProtonSettings(string steamRoot, string protonPath, string gamePath = "", string configPath = "")
    {
        string xlcore = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".xlcore");
        SteamRoot = steamRoot;
        ProtonPath = protonPath;
        GamePath = string.IsNullOrEmpty(gamePath) ? Path.Combine(xlcore, "ffxiv") : gamePath;
        ConfigPath = string.IsNullOrEmpty(configPath) ? Path.Combine(xlcore, "ffxivConfig") : configPath;
    }
}
