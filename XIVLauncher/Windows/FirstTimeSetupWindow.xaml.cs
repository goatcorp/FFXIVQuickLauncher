using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using XIVLauncher.Addon;
using XIVLauncher.Game;
using XIVLauncher.Settings;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class FirstTimeSetup : Window
    {
        public LauncherSettings Result;

        public FirstTimeSetup(LauncherSettings setting)
        {
            InitializeComponent();

            Result = setting;

            var detectedPath = Util.TryGamePaths();

            if (detectedPath != null) GamePathEntry.Text = detectedPath;
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
                if (!Util.IsValidFfxivPath(GamePathEntry.Text))
                {
                    MessageBox.Show("The path you selected is not a valid FFXIV installation", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

            if (SetupTabControl.SelectedIndex == 3)
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

            if (SetupTabControl.SelectedIndex == 5)
            {
                Result.GamePath = new DirectoryInfo(GamePathEntry.Text);
                Result.IsDx11 = Dx11RadioButton.IsChecked == true;
                Result.Language = (ClientLanguage) LanguageComboBox.SelectedIndex;
                Result.InGameAddonEnabled = HooksCheckBox.IsChecked == true;
                Result.SteamIntegrationEnabled = SteamCheckBox.IsChecked == true;

                Result.AddonList = new List<AddonEntry>
                {
                    new AddonEntry
                    {
                        Addon = new RichPresenceAddon(),
                        IsEnabled = RichPresenceCheckBox.IsChecked == true
                    }
                };

                if (ActCheckBox.IsChecked == true)
                {
                    var actPath = FindAct();

                    Result.AddonList.Add(new AddonEntry
                    {
                        IsEnabled = true,
                        Addon = new GenericAddon
                        {
                            Path = actPath
                        }
                    });
                }

                Result.Save();
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