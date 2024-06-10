using System.Collections.Generic;
using System.IO;
using System.Windows;
using CheapLoc;
using IWshRuntimeLibrary;
using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Util;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class FirstTimeSetup : Window
    {
        public bool WasCompleted { get; private set; } = false;

        public FirstTimeSetup()
        {
            InitializeComponent();

            this.DataContext = new FirstTimeSetupViewModel();

            var detectedPath = AppUtil.TryGamePaths();

            if (detectedPath != null) GamePathEntry.Text = detectedPath;

#if !XL_NOAUTOUPDATE
            if (EnvironmentSettings.IsDisableUpdates || AppUtil.GetBuildOrigin() != "goatcorp/FFXIVQuickLauncher")
            {
#endif
                CustomMessageBox.Show(
                    $"You're running an unsupported version of XIVLauncher.\n\nThis can be unsafe and a danger to your SE account. If you have not gotten this unsupported version on purpose, please reinstall a clean version from {App.REPO_URL}/releases and contact us.",
                    "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation, parentWindow: this);
#if !XL_NOAUTOUPDATE
            }
#endif
        }

        public static string GetShortcutTargetFile(string path)
        {
            var shell = new WshShell();
            var shortcut = (IWshShortcut) shell.CreateShortcut(path);

            return shortcut.TargetPath;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (SetupTabControl.SelectedIndex == 0)
            {
                if (string.IsNullOrEmpty(GamePathEntry.Text))
                {
                    CustomMessageBox.Show(Loc.Localize("GamePathEmptyError", "Please select a game path."), "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error, false, false, parentWindow: this);
                    return;
                }

                if (!GameHelpers.LetChoosePath(GamePathEntry.Text))
                {
                    CustomMessageBox.Show(Loc.Localize("GamePathSafeguardError", "Please do not select the \"game\" or \"boot\" folder of your game installation, and choose the folder that contains these instead."), "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: this);
                    return;
                }

                if (!GameHelpers.IsValidGamePath(GamePathEntry.Text))
                {
                    if (CustomMessageBox.Show(Loc.Localize("GamePathInvalidConfirm", "The folder you selected has no installation of the game.\nXIVLauncher will install the game the first time you log in.\nContinue?"), "XIVLauncher",
                            MessageBoxButton.YesNo, MessageBoxImage.Information, parentWindow: this) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                if (GameHelpers.CanMightNotBeInternationalClient(GamePathEntry.Text))
                {
                    if (CustomMessageBox.Show(Loc.Localize("GamePathRegionConfirm", "The folder you selected might be the Chinese or Korean release of the game. XIVLauncher only supports international release of the game.\nIs the folder you've selected indeed for the international version?"), "XIVLauncher",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning, parentWindow: this) != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }

            if (SetupTabControl.SelectedIndex == 2)
            {
                App.Settings.GamePath = new DirectoryInfo(GamePathEntry.Text);
                App.Settings.Language = (ClientLanguage) LanguageComboBox.SelectedIndex;
                App.Settings.InGameAddonEnabled = HooksCheckBox.IsChecked == true;

                App.Settings.AddonList = new List<AddonEntry>();

                WasCompleted = true;
                Close();
            }

            SetupTabControl.SelectedIndex++;
        }
    }
}
