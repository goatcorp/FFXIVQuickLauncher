using System.IO;
using XIVLauncher.Common.Game.Patch.Acquisition;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface ISettings
{
    string AcceptLanguage { get; }
    ClientLanguage? ClientLanguage { get; }
    bool? KeepPatches { get; }
    DirectoryInfo PatchPath { get; }
    DirectoryInfo GamePath { get; }
    AcquisitionMethod? PatchAcquisitionMethod { get; }
    long SpeedLimitBytes { get; }
    int DalamudInjectionDelayMs { get; }
}