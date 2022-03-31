using XIVLauncher.Common;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Core.Configuration;

internal class CommonSettings : ISettings
{
    private readonly ILauncherConfig config;

    public CommonSettings(ILauncherConfig config)
    {
        this.config = config;
    }

    public string AcceptLanguage => this.config.AcceptLanguage;
    public ClientLanguage? ClientLanguage => this.config.ClientLanguage;
    public bool? KeepPatches => false;
    public DirectoryInfo PatchPath => this.config.PatchPath;
    public DirectoryInfo GamePath => this.config.GamePath;
    public AcquisitionMethod? PatchAcquisitionMethod => AcquisitionMethod.Aria;
    public long SpeedLimitBytes => 0;
    public int DalamudInjectionDelayMs => 0;
}