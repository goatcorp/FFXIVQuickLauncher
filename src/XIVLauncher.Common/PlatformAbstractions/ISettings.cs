using System.IO;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface ISettings
{
    string AcceptLanguage { get; }
    ClientLanguage? ClientLanguage { get; }
    bool? KeepPatches { get; }
    DirectoryInfo PatchPath { get; }
    DirectoryInfo GamePath { get; }
    long SpeedLimitBytes { get; }
    int DalamudInjectionDelayMs { get; }
}
