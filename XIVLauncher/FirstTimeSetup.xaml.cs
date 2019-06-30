using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using XIVLauncher.Addon;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class FirstTimeSetup : Window
    {
        public FirstTimeSetup()
        {
            InitializeComponent();

            var detectedPath = Util.TryGamePaths();

            if (detectedPath != null)
            {
                GamePathEntry.Text = detectedPath;
            }
        }

        private string FindAct()
        {
            try
            {
                RegistryKey parentKey =
                    Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");

                var nameList = parentKey.GetSubKeyNames();
                foreach (var name in nameList)
                {
                    var regKey = parentKey.OpenSubKey(name);

                    var value = regKey.GetValue("DisplayName");
                    if (value != null && value.ToString() == "Advanced Combat Tracker (remove only)")
                    {
                        return System.IO.Path.GetDirectoryName(regKey.GetValue("UninstallString").ToString()
                            .Replace("\"", string.Empty));
                    }
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
                if (!Util.IsValidFFXIVPath(GamePathEntry.Text))
                {
                    MessageBox.Show("The path you selected is not a valid FFXIV installation", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
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
                Settings.SetGamePath(GamePathEntry.Text);
                Settings.SetDx11(Dx11RadioButton.IsChecked == true);
                Settings.SetLanguage((ClientLanguage) LanguageComboBox.SelectedIndex);
                Settings.SetInGameAddonEnabled(HooksCheckBox.IsChecked == true);

                var addonList = new List<AddonEntry>()
                {
                    new AddonEntry()
                    {
                        Addon = new RichPresenceAddon(),
                        IsEnabled = RichPresenceCheckBox.IsChecked == true
                    }
                };

                if (ActCheckBox.IsChecked == true)
                {
                    var actPath = FindAct();

                    addonList.Add(new AddonEntry()
                    {
                        IsEnabled = true,
                        Addon = new GenericAddon()
                        {
                            Path = actPath
                        }
                    });
                }

                Settings.SetAddonList(addonList);

                Settings.Save();
                Close();
            }

            SetupTabControl.SelectedIndex++;
        }
    }
}
