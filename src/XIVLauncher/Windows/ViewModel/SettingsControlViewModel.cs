using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using CheapLoc;

namespace XIVLauncher.Windows.ViewModel
{
    class SettingsControlViewModel : INotifyPropertyChanged
    {
        private string _gamePath;
        private string _patchPath;

        public SettingsControlViewModel()
        {
            SetupLoc();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Gets a value indicating whether the "Run Integrity Checks" button is enabled.
        /// </summary>
        public bool IsRunIntegrityCheckPossible =>
            !string.IsNullOrEmpty(GamePath) && Directory.Exists(GamePath);

        /// <summary>
        /// Gets or sets the path to the game folder.
        /// </summary>
        public string GamePath
        {
            get => _gamePath;
            set
            {
                _gamePath = value;
                OnPropertyChanged(nameof(GamePath));
                OnPropertyChanged(nameof(IsRunIntegrityCheckPossible));
            }
        }

        /// <summary>
        /// Gets or sets the path to the game folder.
        /// </summary>
        public string PatchPath
        {
            get => _patchPath;
            set
            {
                _patchPath = value;
                OnPropertyChanged(nameof(PatchPath));
            }
        }

        private void SetupLoc()
        {
            OpenPluginsFolderLoc = Loc.Localize("OpenPluginsFolder", "Open Plugins Folder");
            SaveSettingsLoc = Loc.Localize("SaveSettings", "Save Settings");

            SettingsGameLoc = Loc.Localize("SettingsGame", "Game");
            GamePathLoc = Loc.Localize("ChooseGamePath",
                "Please select the folder your game is installed in.\r\nIt should contain the folders \"game\" and \"boot\".");
            GamePathSafeguardLoc = Loc.Localize("GamePathSafeguardError",
                "Please do not select the \"game\" or \"boot\" folder of your game installation, and choose the folder that contains these instead.");
            GamePathSafeguardRegionLoc = Loc.Localize("GamePathSafeguardRegionWarning",
                "XIVLauncher does not support Chinese or Korean version of the game. Make sure this path indeed is for the international version.");
            SteamCheckBoxLoc = Loc.Localize("FirstTimeSteamCheckBox", "Enable Steam integration");
            OtpServerCheckBoxLoc = Loc.Localize("OtpServerCheckBox", "Enable XL Authenticator app/OTP macro support");
            OtpServerTooltipLoc = Loc.Localize("OtpServerTooltip", "This will allow you to send your OTP code to XIVLauncher directly from your phone.\nClick \"Learn more\" to see how to set this up.");
            LearnMoreLoc = Loc.Localize("LearnMore", "Learn More");
            OtpLearnMoreTooltipLoc = Loc.Localize("OtpLearnMoreTooltipLoc", "Open a guide in your web browser.");
            OtpAlwaysOnTopCheckBoxLoc = Loc.Localize("OtpAlwaysOnTopCheckBox", "Keep the OTP Window Always on Top");
            OtpAlwaysOnTopTooltipLoc = Loc.Localize("OtpAlwaysOnTopTooltip", "This will keep the One Time Password Popup ontop of any window, even if it looses focus.");
            AdditionalArgumentsLoc = Loc.Localize("AdditionalArguments", "Additional launch arguments");
            ChooseDpiAwarenessLoc = Loc.Localize("ChooseDpiAwareness", "Game DPI Awareness");
            DpiAwarenessAwareLoc = Loc.Localize("DpiAwarenessAware", "Aware");
            DpiAwarenessUnawareLoc = Loc.Localize("DpiAwarenessUnaware", "Unaware");
            ChooseDpiAwarenessHintLoc = Loc.Localize("ChooseDpiAwarenessHint", "If game scaling appears wrong when using XIVLauncher, please attempt changing this setting.");
            RunIntegrityCheckLoc = Loc.Localize("RunIntegrityCheck", "Run integrity check");
            RunIntegrityCheckTooltipLoc =
                Loc.Localize("RunIntegrityCheckTooltip", "Run integrity check on game files.");
            AutoStartSteamLoc = Loc.Localize("AutoStartSteam", "Start Steam when starting XIVLauncher");
            AutoStartSteamTooltipLoc = Loc.Localize("AutoStartSteamTooltip", "Whenever you open XIVLauncher, it will check if Steam is running and start it if it isn't.\nYou will automatically show as \"Playing\" on Steam.");

            SettingsGameSettingsLoc = Loc.Localize("SettingsGameSettings", "Game Settings");
            ChooseLanguageLoc = Loc.Localize("ChooseLanguage", "Please select which language you want to load the game with.");
            ChooseLauncherLanguageLoc = Loc.Localize("ChooseLauncherLanguage", "Please select the launcher language, requires a restart.");
            LauncherLanguageHelpCtaLoc = Loc.Localize("LauncherLanguageHelpCtaLoc",
                "Notice any mistakes? You can help out translating the launcher! Just click here!");
            LauncherLanguageNoticeLoc = Loc.Localize("LauncherLanguageNotice", "A restart is required to apply the launcher language setting.");

            SettingsAutoLaunchLoc = Loc.Localize("SettingsAutoLaunch", "Auto-Launch");
            AutoLaunchHintLoc = Loc.Localize("AutoLaunchHint",
                "These are applications that are started as soon as the game has started.");
            RemoveLoc = Loc.Localize("Remove", "Remove");
            AddNewLoc = Loc.Localize("AddNew", "Add new");
            AutoLaunchAddNewToolTipLoc =
                Loc.Localize("AutoLaunchAddNewToolTip", "Add a new Auto-Start entry that allows you to launch any program.");

            SettingsInGameLoc = Loc.Localize("SettingsInGame", "Dalamud");
            InGameAddonDescriptionLoc = Loc.Localize("InGameAddonDescription",
                "These options affect Dalamud, the XIVLauncher plugin loader.\nDalamud will be enabled if the version of the game is compatible, and the checkbox below is ticked.");
            InGameAddonCommandHintLoc = Loc.Localize("InGameAddonCommandHint",
                "When enabled, hover the red moon logo on the title screen for options.");
            InGameAddonEnabledCheckBoxLoc = Loc.Localize("InGameAddonEnabledCheckBox", "Enable Dalamud");
            InGameAddonInjectionDelayLoc = Loc.Localize("InGameAddonInjectionDelayLoc", "Addon Injection Delay");
            InGameAddonInjectionDelayDescriptionLoc = Loc.Localize("InGameAddonInjectionDelayDescriptionLoc",
                "Delay the injection of the in-game addon. This allows you to hide it from e.g. OBS and Discord, since they will inject before it does.");
            InGameAddonBranchSwitcherDescriptionLoc = Loc.Localize("InGameAddonBranchSwitcherDescriptionLoc",
                "Dalamud has multiple release channels. Click the button below to switch between them.\nThis can be used to test new features or opt into beta testing releases for new patches.");
            InGameAddonBranchSwitcherCurrentBranchLoc = Loc.Localize("InGameAddonBranchSwitcherCurrentBranchLoc",
                "Currently selected branch: ");

            InGameAddonLoadMethodLoc = Loc.Localize("InGameAddonLoadMethodLoc",
                "Choose how to load Dalamud.");
            InGameAddonLoadMethodEntryPointLoc = Loc.Localize("InGameAddonLoadMethodEntryPointLoc",
                "New: improves compatibility with certain other software and plugins that need to load early.");
            InGameAddonLoadMethodDllInjectLoc = Loc.Localize("InGameAddonLoadMethodDllInjectLoc",
                "Legacy: old version of the Dalamud injection system that may be more stable.");
            InGameAddonLoadMethodEntryPointDescriptionLoc = Loc.Localize("InGameAddonLoadMethodEntryPointDescriptionLoc",
                "This method uses Entry-Point rewriting to load Dalamud, which may be more compatible with certain other software, like anti-virus tools.");
            InGameAddonLoadMethodDllInjectDescriptionLoc = Loc.Localize("InGameAddonLoadMethodDllInjectDescriptionLoc",
                "This method loads Dalamud via DLL-injection, which may be more stable on certain systems.");

            SettingsPatchingLoc = Loc.Localize("SettingsPatching", "Patching");
            AskBeforePatchLoc = Loc.Localize("AskBeforePatch", "Ask before installing a game patch");
            PatchPathLoc = Loc.Localize("PatchPath", "Patch Download Directory");
            PatchSpeedLimitLoc = Loc.Localize("PatchSpeedLimit", "Download Speed Limit");
            KeepPatchesLoc = Loc.Localize("KeepPatches", "Keep downloaded patches for future reinstalls");
            this.ChoosePatchAcquisitionMethodLoc = Loc.Localize("PatchAcquisitionMethod", "Choose the method used to acquire patches. We recommend \"ARIA(HTTP)\".");

            SettingsAboutLoc = Loc.Localize("SettingsAbout", "About");
            CreditsLoc = Loc.Localize("Credits",
                "Made by goat. Special thanks to Mino, sky, LeonBlade, Wintermute, Zyian,\r\nRoy, Meli, Aida Enna, Aireil, kizer and the angry paissa artist!\r\n\r\nAny issues or requests? Join the Discord or create an issue on GitHub!");
            LicenseLoc = Loc.Localize("License", "Licensed under GPLv3 or later. Click here for more.");
            JoinDiscordLoc = Loc.Localize("JoinDiscord", "Join Discord");
            OpenFaqLoc = Loc.Localize("OpenFaq", "Open FAQ");
            StartBackupToolLoc = Loc.Localize("StartBackupTool", "Start Backup Tool");
            StartOriginalLauncherLoc = Loc.Localize("StartOriginalLauncher", "Start Original Launcher");
            IsFreeTrialLoc = Loc.Localize("IsFreeTrial", "Using free trial account");

            OpenAdvancedSettingsLoc = Loc.Localize("OpenAdvancedSettings", "Open Advanced Settings");
            OpenAdvancedSettingsTipLoc = Loc.Localize("OpenAdvancedSettingsTip", "Opens some settings for advanced users. Please only use these if you know what you're doing.");

            OpenDalamudBranchSwitcherLoc = Loc.Localize("OpenDalamudBranchSwitcher", "Switch Dalamud Branch");
            OpenDalamudBranchSwitcherTipLoc = Loc.Localize("OpenDalamudBranchSwitcherTip", "Open an interface that lets you opt into testing releases for Dalamud.");

            PluginDisabledTagLoc = Loc.Localize("DisabledPlugin", " (disabled)");
        }

        public string OpenPluginsFolderLoc { get; private set; }
        public string SaveSettingsLoc { get; private set; }

        public string SettingsGameLoc { get; private set; }
        public string GamePathLoc { get; private set; }
        public string GamePathSafeguardLoc { get; private set; }
        public string GamePathSafeguardRegionLoc { get; private set; }
        public string SteamCheckBoxLoc { get; private set; }
        public string OtpServerCheckBoxLoc { get; private set; }
        public string OtpServerTooltipLoc { get; private set; }
        public string LearnMoreLoc { get; private set; }
        public string OtpLearnMoreTooltipLoc { get; private set; }
        public string OtpAlwaysOnTopCheckBoxLoc { get; private set; }
        public string OtpAlwaysOnTopTooltipLoc { get; private set; }
        public string AdditionalArgumentsLoc { get; private set; }
        public string ChooseDpiAwarenessLoc { get; private set; }
        public string ChooseDpiAwarenessHintLoc { get; private set; }
        public string DpiAwarenessAwareLoc { get; private set; }
        public string DpiAwarenessUnawareLoc { get; private set; }
        public string RunIntegrityCheckLoc { get; private set; }
        public string RunIntegrityCheckTooltipLoc { get; private set; }
        public string AutoStartSteamLoc { get; private set; }
        public string AutoStartSteamTooltipLoc { get; private set; }

        public string SettingsGameSettingsLoc { get; private set; }
        public string ChooseLanguageLoc { get; private set; }
        public string ChooseLauncherLanguageLoc { get; private set; }
        public string LauncherLanguageHelpCtaLoc { get; private set; }
        public string LauncherLanguageNoticeLoc { get; private set; }

        public string SettingsAutoLaunchLoc { get; private set; }
        public string AutoLaunchHintLoc { get; private set; }
        public string RemoveLoc { get; private set; }
        public string AddNewLoc { get; private set; }
        public string AutoLaunchAddNewToolTipLoc { get; private set; }

        public string SettingsInGameLoc { get; private set; }
        public string InGameAddonDescriptionLoc { get; private set; }
        public string InGameAddonCommandHintLoc { get; private set; }
        public string InGameAddonEnabledCheckBoxLoc { get; private set; }
        public string InGameAddonInjectionDelayLoc { get; private set; }
        public string InGameAddonInjectionDelayDescriptionLoc { get; private set; }
        public string InGameAddonLoadMethodLoc { get; private set; }
        public string InGameAddonLoadMethodEntryPointLoc { get; private set; }
        public string InGameAddonLoadMethodDllInjectLoc { get; private set; }
        public string InGameAddonLoadMethodEntryPointDescriptionLoc { get; private set; }
        public string InGameAddonLoadMethodDllInjectDescriptionLoc { get; private set; }
        public string InGameAddonBranchSwitcherDescriptionLoc { get; private set; }
        public string InGameAddonBranchSwitcherCurrentBranchLoc { get; private set; }

        public string SettingsPatchingLoc { get; private set; }
        public string AskBeforePatchLoc { get; private set; }
        public string PatchPathLoc { get; private set; }
        public string PatchSpeedLimitLoc { get; private set; }
        public string KeepPatchesLoc { get; private set; }
        public string ChoosePatchAcquisitionMethodLoc { get; private set; }

        public string SettingsAboutLoc { get; private set; }
        public string CreditsLoc { get; private set; }
        public string LicenseLoc { get; private set; }
        public string JoinDiscordLoc { get; private set; }
        public string OpenFaqLoc { get; private set; }
        public string StartBackupToolLoc { get; private set; }
        public string StartOriginalLauncherLoc { get; private set; }
        public string IsFreeTrialLoc { get; private set; }
        public string OpenAdvancedSettingsLoc { get; private set; }
        public string OpenAdvancedSettingsTipLoc { get; private set; }
        public string OpenDalamudBranchSwitcherLoc { get; private set; }
        public string OpenDalamudBranchSwitcherTipLoc { get; private set; }

        public string PluginDisabledTagLoc { get; private set; }
    }
}
