using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CheapLoc;
using Serilog;

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
                "Please do not select the \"game\" or \"boot\" folder of your FFXIV installation, and choose the folder that contains these instead.");
            SteamCheckBoxLoc = Loc.Localize("FirstTimeSteamCheckBox", "Enable Steam integration");
            OtpServerCheckBoxLoc = Loc.Localize("OtpServerCheckBox", "Enable XL Authenticator app/OTP macro support");
            AdditionalArgumentsLoc = Loc.Localize("AdditionalArguments", "Additional launch arguments");
            RunIntegrityCheckLoc = Loc.Localize("RunIntegrityCheck", "Run integrity check");
            RunIntegrityCheckTooltipLoc =
                Loc.Localize("RunIntegrityCheckTooltip", "Run integrity check on game files.");

            SettingsGameSettingsLoc = Loc.Localize("SettingsGameSettings", "Game Settings");
            DirectXLoc = Loc.Localize("ChooseDirectX", "Please select which DirectX version you want to use.");
            DirectX9NoticeLoc = Loc.Localize("DirectX9Notice",
                "DirectX 9 mode is not supported anymore. It will still start, but you will not get support from\r\nSE for any technical issues any additional XIVLauncher features such as Rich Presence and the\r\nIn-Game addon will not work.");
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

            SettingsInGameLoc = Loc.Localize("SettingsInGame", "In-Game");
            InGameAddonDescriptionLoc = Loc.Localize("InGameAddonDescription",
                "These options affect the XIVLauncher in-game features. These features will be automatically\r\nenabled if you are running the DirectX 11 version of the game, the version of the game is\r\ncompatible, and the checkbox below is ticked.");
            InGameAddonCommandHintLoc = Loc.Localize("InGameAddonCommandHint",
                "When enabled, type \"/xlhelp\" in-game to see other available commands.");
            InGameAddonEnabledCheckBoxLoc = Loc.Localize("InGameAddonEnabledCheckBox", "Enable in-game features");
            InGameAddonChatSettingsLoc = Loc.Localize("ChatSettings", "Chat settings");
            InGameAddonDiscordBotTokenLoc = Loc.Localize("DiscordBotToken", "Discord Bot Token");
            InGameAddonHowLoc = Loc.Localize("HowToHint", "How do I set this up?");
            InGameAddonAddChatChannelLoc = Loc.Localize("AddChatChannel", "Add chat channel");
            InGameAddonSetCfChannelLoc = Loc.Localize("InGameAddonSetCfChannel", "Set Duty Finder notification channel");
            InGameAddonSetRouletteChannelLoc = Loc.Localize("InGameAddonSetRouletteChannel", "Set Roulette Bonus notification channel");
            InGameAddonSetRetainerChannelLoc = Loc.Localize("InGameAddonSetRetainerChannel", "Set retainer sale channel");
            InGameAddonChatDelayLoc = Loc.Localize("InGameAddonChatDelay", "Chat Post Delay");
            InGameAddonChatDelayDescriptionLoc = Loc.Localize("InGameAddonChatDelayDescription",
                "Check for recently sent messages to avoid duplicates.\r\nThis allows for multiple users to use the same channel as a chat log.\r\nPlease set an appropriate delay in milliseconds below(e.g. 1000).");
            InGameAddonInjectionDelayLoc = Loc.Localize("InGameAddonInjectionDelayLoc", "Addon Injection Delay");
            InGameAddonInjectionDelayDescriptionLoc = Loc.Localize("InGameAddonInjectionDelayDescriptionLoc",
                "Delay the injection of the in-game addon. This allows you to hide it from e.g. OBS and Discord, since they will inject before it does.");
            UniversalisHintLoc = Loc.Localize("UniversalisHint",
                "Market board data provided in cooperation with Universalis.");
            UniversalisOptOutLoc = Loc.Localize("UniversalisOptOut",
                "Opt-out of contributing anonymously to crowd-sourced market board information");

            PluginsDescriptionLoc = Loc.Localize("PluginsDescriptionLoc",
                "These are the plugins that are currently available installed on your machine.");
            PluginsToggleLoc = Loc.Localize("Toggle", "Toggle");
            PluginsDeleteLoc = Loc.Localize("Delete", "Delete");
            PluginsInstallHintLoc =
                Loc.Localize("PluginsInstallHint", "You can use the /xlplugins command in-game to install more plugins.");

            SettingsPatchingLoc = Loc.Localize("SettingsPatching", "Patching");
            AskBeforePatchLoc = Loc.Localize("AskBeforePatch", "Ask before installing a game patch");
            PatchPathLoc = Loc.Localize("PatchPath", "Patch Download Directory");
            PatchSpeedLimitLoc = Loc.Localize("PatchSpeedLimit", "Download Speed Limit");
            KeepPatchesLoc = Loc.Localize("KeepPatches", "Keep downloaded patches for future reinstalls");
            this.ChoosePatchAcquisitionMethodLoc = Loc.Localize("PatchAcquisitionMethod", "Choose the method used to acquire patches. We recommend \"ARIA(HTTP)\".");

            SettingsAboutLoc = Loc.Localize("SettingsAbout", "About");
            CreditsLoc = Loc.Localize("Credits",
                "Made by goat. Special thanks to Mino, sky, LeonBlade, Wintermute, Zyian,\r\nRoy, Meli, Aida Enna, Aireil, and the angry paissa artist!\r\n\r\nAny issues or requests? Join the Discord or create an issue on GitHub!");
            LicenseLoc = Loc.Localize("License", "Licensed under GPLv3 or later. Click here for more.");
            JoinDiscordLoc = Loc.Localize("JoinDiscord", "Join Discord");
            OpenFaqLoc = Loc.Localize("OpenFaq", "Open FAQ");
            StartBackupToolLoc = Loc.Localize("StartBackupTool", "Start Backup Tool");
            StartOriginalLauncherLoc = Loc.Localize("StartOriginalLauncher", "Start Original Launcher");
            EnabledUidCacheLoc = Loc.Localize("EnabledUidCache", "Enable experimental UID cache (this will break on game updates!)");
            ResetUidCacheTipLoc = Loc.Localize("ResetUidCacheTip", "Hold control while starting the launcher to reset the UID cache");
            EnableEncryptionLoc = Loc.Localize("EnableEncryption", "Enable encrypting arguments to the client");
        }

        public string OpenPluginsFolderLoc { get; private set; }
        public string SaveSettingsLoc { get; private set; }

        public string SettingsGameLoc { get; private set; }
        public string GamePathLoc { get; private set; }
        public string GamePathSafeguardLoc { get; private set; }
        public string SteamCheckBoxLoc { get; private set; }
        public string OtpServerCheckBoxLoc { get; private set; }
        public string AdditionalArgumentsLoc { get; private set; }
        public string RunIntegrityCheckLoc { get; private set; }
        public string RunIntegrityCheckTooltipLoc { get; private set; }

        public string SettingsGameSettingsLoc { get; private set; }
        public string DirectXLoc { get; private set; }
        public string DirectX9NoticeLoc { get; private set; }
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
        public string InGameAddonChatSettingsLoc { get; private set; }
        public string InGameAddonDiscordBotTokenLoc { get; private set; }
        public string InGameAddonHowLoc { get; private set; }
        public string InGameAddonAddChatChannelLoc { get; private set; }
        public string InGameAddonSetCfChannelLoc { get; private set; }
        public string InGameAddonSetRouletteChannelLoc { get; private set; }
        public string InGameAddonSetRetainerChannelLoc { get; private set; }
        public string InGameAddonChatDelayLoc { get; private set; }
        public string InGameAddonChatDelayDescriptionLoc { get; private set; }
        public string InGameAddonInjectionDelayLoc { get; private set; }
        public string InGameAddonInjectionDelayDescriptionLoc { get; private set; }
        public string UniversalisHintLoc { get; private set; }
        public string UniversalisOptOutLoc { get; private set; }

        public string PluginsDescriptionLoc { get; private set; }
        public string PluginsToggleLoc { get; private set; }
        public string PluginsDeleteLoc { get; private set; }
        public string PluginsInstallHintLoc { get; private set; }

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
        public string EnabledUidCacheLoc { get; private set; }
        public string ResetUidCacheTipLoc { get; private set; }
        public string EnableEncryptionLoc { get; private set; }
    }
}
