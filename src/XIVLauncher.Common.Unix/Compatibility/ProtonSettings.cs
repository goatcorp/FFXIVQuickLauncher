using System;
using System.Collections.Generic;
using System.IO;

namespace XIVLauncher.Common.Unix.Compatibility;

public class ProtonSettings
{
    public DirectoryInfo Prefix { get; }
    
    public string SteamRoot { get; }

    public string ProtonPath { get; }

    public string SteamLibrary => Path.Combine(SteamRoot, "steamapps", "common");

    public string SoldierRun => Path.Combine(SteamLibrary, "SteamLinuxRuntime_soldier", "run");

    public string SoldierInject => Path.Combine(SteamLibrary, "SteamLinuxRuntime_soldier","_v2-entry-point");

    public string GamePath { get; }

    public string ConfigPath { get; }

    public bool UseReaper { get; }

    public bool UseSoldier { get; }

    public string ReaperPath => Path.Combine(SteamRoot,"ubuntu12_32","reaper");

    public string CompatMounts => GamePath + ':' + ConfigPath;

    public string SteamAppId { get; }

    public ProtonSettings(DirectoryInfo protonPrefix, string steamRoot, string protonPath, string gamePath = "", string configPath = "", string appId = "39210", bool useSoldier = true, bool useReaper = false)
    {
        Prefix = protonPrefix;
        string xlcore = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".xlcore");
        SteamRoot = steamRoot;
        ProtonPath = Path.Combine(protonPath, "proton");
        GamePath = string.IsNullOrEmpty(gamePath) ? Path.Combine(xlcore, "ffxiv") : gamePath;
        ConfigPath = string.IsNullOrEmpty(configPath) ? Path.Combine(xlcore, "ffxivConfig") : configPath;
#if FLATPAK
        UseSoldier = false; // Already in a flatpak container, so this is ignored. Pressure-vessel and flatpak don't like to share.
#else
        UseSoldier = useSoldier;
#endif
        UseReaper = useReaper;
        SteamAppId = appId;
    }

    public string GetCommand(bool inject = true)
    {
#if FLATPAK
        inject = true;
#endif
        if (UseReaper) return ReaperPath;
        if (UseSoldier) return inject ? SoldierInject : SoldierRun;
        return ProtonPath;   
    }

    public string GetArguments(bool inject = true, string verb = "runinprefix")
    {
        List<string> commands = new List<string>();
        if (UseReaper)
        {
            commands.Add("SteamLaunch --");
        }
        if (UseSoldier)
        {
            if (UseReaper) commands.Add(inject ? SoldierInject : SoldierRun);
            commands.Add(inject ? "--verb=waitforexitandrun --" : "--");
        }
        if (UseSoldier || UseReaper)
            commands.Add("\"" + ProtonPath + "\"");
        commands.Add(verb);

        return string.Join(' ', commands);        
    }

    public string[] GetArgumentsAsArray(bool inject = true, string verb = "runinprefix")
    {
        List<string> commands = new List<string>();
        if (UseReaper)
        {
            commands.Add("SteamLaunch");
            commands.Add("--");
        }
        if (UseSoldier)
        {
            if (UseReaper) commands.Add(inject ? SoldierInject : SoldierRun);
            if (inject) commands.Add("--verb=waitforexitandrun");
            commands.Add("--");

        }
        if (UseSoldier || UseReaper)
            commands.Add(ProtonPath);
        commands.Add(verb);

        return commands.ToArray();
    }
}
