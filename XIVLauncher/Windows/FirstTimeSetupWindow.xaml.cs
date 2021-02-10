using System.Collections.Generic;
using System.IO;
using System.Windows;
using CheapLoc;
using Microsoft.Win32;
using XIVLauncher.Addon;
using XIVLauncher.Game;
using XIVLauncher.Settings;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class FirstTimeSetup : Window
    {
        public FirstTimeSetup()
        {
            InitializeComponent();

            this.DataContext = new FirstTimeSetupViewModel();

            var detectedPath = Util.TryGamePaths();

            if (detectedPath != null) GamePathEntry.Text = detectedPath;

#if XL_NOAUTOUPDATE
            MessageBox.Show(
                $"You're running an unsupported version of XIVLauncher.\n\nThis can be unsafe and a danger to your SE account. If you have not gotten this unsupported version on purpose, please reinstall a clean version from {App.RepoUrl}/releases.",
                "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
#endif
        }

        private string FindAct()
        {
            try
            {
                var parentKey =
                    Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");

                var nameList = parentKey.GetSubKeyNames();
                foreach (var name in nameList)
                {
                    var regKey = parentKey.OpenSubKey(name);

                    var value = regKey.GetValue("DisplayName");
                    if (value != null && value.ToString() == "Advanced Combat Tracker (remove only)")
                        return Path.GetDirectoryName(regKey.GetValue("UninstallString").ToString()
                            .Replace("\"", string.Empty));
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (SetupTabControl.SelectedIndex == 0)
            {
                if (string.IsNullOrEmpty(GamePathEntry.Text))
                {
                    MessageBox.Show(Loc.Localize("GamePathEmptyError", "Please select a game path."), "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Util.LetChoosePath(GamePathEntry.Text))
                {
                    MessageBox.Show(Loc.Localize("GamePathSafeguardError", "Please do not select the \"game\" or \"boot\" folder of your FFXIV installation, and choose the folder that contains these instead."), "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Util.IsValidFfxivPath(GamePathEntry.Text))
                {
                    MessageBox.Show(Loc.Localize("GamePathInvalidError", "The folder you selected has no FFXIV installation.\nXIVLauncher will install FFXIV the first time you log in."), "XIVLauncher",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            if (SetupTabControl.SelectedIndex == 2)
            {
                // Check if ACT is installed, if it isn't, just skip this step
                var actPath = FindAct();

                if (actPath == null)
                {
                    SetupTabControl.SelectedIndex++;
                    NextButton_Click(null, null);
                    return;
                }
            }

            if (SetupTabControl.SelectedIndex == 4)
            {
                App.Settings.GamePath = new DirectoryInfo(GamePathEntry.Text);
                App.Settings.IsDx11 = Dx11RadioButton.IsChecked == true;
                App.Settings.Language = (ClientLanguage) LanguageComboBox.SelectedIndex;
                App.Settings.InGameAddonEnabled = HooksCheckBox.IsChecked == true;
                App.Settings.SteamIntegrationEnabled = SteamCheckBox.IsChecked == true;

                App.Settings.AddonList = new List<AddonEntry>();

                if (ActCheckBox.IsChecked == true)
                {
                    var actPath = FindAct();

                    App.Settings.AddonList.Add(new AddonEntry
                    {
                        IsEnabled = true,
                        Addon = new GenericAddon
                        {
                            Path = actPath
                        }
                    });
                }

                Close();
            }

            SetupTabControl.SelectedIndex++;
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            Dx9DisclaimerTextBlock.Visibility = Visibility.Visible;
        }

        private void Dx9RadioButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            Dx9DisclaimerTextBlock.Visibility = Visibility.Hidden;
        }
    }
}