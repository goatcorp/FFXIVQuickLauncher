using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Core.Configuration;

public interface ILauncherConfig
{
    public bool? CompletedFts { get; set; }

    public bool? DoVersionCheck { get; set; }

    public float? FontPxSize { get; set; }

    public string? CurrentAccountId { get; set; }

    public string? AcceptLanguage { get; set; }

    public bool? IsAutologin { get; set; }

    public DirectoryInfo? GamePath { get; set; }

    public DirectoryInfo? GameConfigPath { get; set; }

    public string? AdditionalArgs { get; set; }

    public ClientLanguage? ClientLanguage { get; set; }

    public bool? IsUidCacheEnabled { get; set; }

    public float? GlobalScale { get; set; }

    public DpiAwareness? DpiAwareness { get; set; }

    public bool? TreatNonZeroExitCodeAsFailure { get; set; }

    public List<AddonEntry>? Addons { get; set; }

    public bool? IsDx11 { get; set; }

    public bool? IsEncryptArgs { get; set; }

    public bool? IsFt { get; set; }

    public bool? IsOtpServer { get; set; }

    #region Patching

    public DirectoryInfo? PatchPath { get; set; }

    public bool? KeepPatches { get; set; }

    public AcquisitionMethod? PatchAcquisitionMethod { get; set; }

    public long PatchSpeedLimit { get; set; }

    #endregion

    #region Linux

    public WineStartupType? WineStartupType { get; set; }

    public string? WineBinaryPath { get; set; }

    public bool? GameModeEnabled { get; set; }

    public bool? DxvkAsyncEnabled { get; set; }

    public bool? ESyncEnabled { get; set; }

    public bool? FSyncEnabled { get; set; }

    public Dxvk.DxvkHudType DxvkHudType { get; set; }

    public string? WineDebugVars { get; set; }

    #endregion

    #region Dalamud

    public bool? DalamudEnabled { get; set; }

    public DalamudLoadMethod? DalamudLoadMethod { get; set; }

    public int DalamudLoadDelay { get; set; }

    #endregion
}