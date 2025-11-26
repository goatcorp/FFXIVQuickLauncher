using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;

namespace XIVLauncher.NativeAOT.Configuration;

public enum License
{
    Windows,
    Mac,
    Steam
}

public class LauncherConfig
{
    public string? AcceptLanguage { get; set; }

    public DirectoryInfo? GamePath { get; set; }

    public DirectoryInfo? GameConfigPath { get; set; }

    public string? AdditionalArgs { get; set; }

    public ClientLanguage? ClientLanguage { get; set; }

    public bool? IsUidCacheEnabled { get; set; }

    public bool? IsEncryptArgs { get; set; }

    public License? License { get; set; }

    public bool? IsFt { get; set; }

    public bool? IsAutoLogin { get; set; }

    public DpiAwareness? DpiAwareness { get; set; }

    #region Patching

    public DirectoryInfo? PatchPath { get; set; }

    public bool? KeepPatches { get; set; }

    public AcquisitionMethod? PatchAcquisitionMethod { get; set; }

    public long PatchSpeedLimit { get; set; }

    #endregion

    #region Dalamud

    public bool? DalamudEnabled { get; set; }

    public DalamudLoadMethod? DalamudLoadMethod { get; set; }

    public int DalamudLoadDelay { get; set; }

    #endregion
}
