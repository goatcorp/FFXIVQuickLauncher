using XIVLauncher.Common;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.NativeAOT.Configuration;

public class CommonSettings : ISettings
{
    private readonly LauncherConfig config = Program.Config!;

    private static CommonSettings? instance;

    public static CommonSettings? Instance
    {
        get
        {
            instance ??= new CommonSettings();
            return instance;
        }
    }

    public string AcceptLanguage => config.AcceptLanguage!;
    public ClientLanguage? ClientLanguage => config.ClientLanguage;
    public bool? KeepPatches => config.KeepPatches;
    public DirectoryInfo PatchPath => config.PatchPath!;
    public DirectoryInfo GamePath => config.GamePath!;
    public AcquisitionMethod? PatchAcquisitionMethod => AcquisitionMethod.NetDownloader;
    public long SpeedLimitBytes => config.PatchSpeedLimit;
    public int DalamudInjectionDelayMs => config.DalamudLoadDelay;
}
