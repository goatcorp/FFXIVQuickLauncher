using System.Collections.Generic;
using System.IO;
using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;

namespace XIVLauncher.Settings
{
    public interface ILauncherSettingsV3
    {
        #region Launcher Setting

        DirectoryInfo GamePath { get; set; }
        bool IsDx11 { get; set; }
        bool AutologinEnabled { get; set; }
        List<AddonEntry> AddonList { get; set; }
        bool UniqueIdCacheEnabled { get; set; }
        string AdditionalLaunchArgs { get; set; }
        bool InGameAddonEnabled { get; set; }
        DalamudLoadMethod? InGameAddonLoadMethod { get; set; }
        bool OtpServerEnabled { get; set; }
        ClientLanguage? Language { get; set; }
        LauncherLanguage? LauncherLanguage { get; set; }
        string CurrentAccountId { get; set; }
        bool? EncryptArguments { get; set; }
        DirectoryInfo PatchPath { get; set; }
        bool? AskBeforePatchInstall { get; set; }
        long SpeedLimitBytes { get; set; }
        decimal DalamudInjectionDelayMs { get; set; }
        bool? KeepPatches { get; set; }
        bool? HasComplainedAboutAdmin { get; set; }
        bool? HasComplainedAboutGShadeDxgi { get; set; }
        string LastVersion { get; set; }
        AcquisitionMethod? PatchAcquisitionMethod { get; set; }
        bool? HasShownAutoLaunchDisclaimer { get; set; }
        string AcceptLanguage { get; set; }
        DpiAwareness? DpiAwareness { get; set; }
        int? VersionUpgradeLevel { get; set; }
        bool? TreatNonZeroExitCodeAsFailure { get; set; }
        bool? ExitLauncherAfterGameExit { get; set; }
        bool? IsFt { get; set; }
        string DalamudRolloutBucket { get; set; }

        #endregion
    }
}