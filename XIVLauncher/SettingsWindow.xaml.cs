using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using XIVLauncher.Addon;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            GamePathEntry.Text = Settings.GetGamePath();
            Dx11RadioButton.IsChecked = Settings.IsDX11();
            ExpansionLevelComboBox.SelectedIndex = Settings.GetExpansionLevel();
            LanguageComboBox.SelectedIndex = (int) Settings.GetLanguage();
            AddonListView.ItemsSource = Settings.GetAddonList();

            VersionLabel.Text += " - v" + Util.GetAssemblyVersion() + " - " + Util.GetGitHash();
        }

        private void SettingsWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Settings.SetGamePath(GamePathEntry.Text);
            Settings.SetDx11(Dx11RadioButton.IsChecked == true);
            Settings.SetExpansionLevel(ExpansionLevelComboBox.SelectedIndex);
            Settings.SetLanguage((ClientLanguage) LanguageComboBox.SelectedIndex);
            Settings.SetAddonList((List<AddonEntry>) AddonListView.ItemsSource);
            Settings.Save();
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/goaaats/FFXIVQuickLauncher");
        }

        private void BackupToolButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(System.IO.Path.Combine(GamePathEntry.Text, "boot", "ffxivconfig.exe"));
        }

        private void OriginalLauncherButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(System.IO.Path.Combine(GamePathEntry.Text, "boot", "ffxivboot.exe"));
        }

        // All of the list handling is very dirty - but i guess it works

        private void AddAddon_OnClick(object sender, RoutedEventArgs e)
        {
            var addonSetup = new GenericAddonSetup();
            addonSetup.ShowDialog();

            if (addonSetup.Result != null)
            {
                var addonList = Settings.GetAddonList();

                addonList.Add(new AddonEntry()
                {
                    IsEnabled = true,
                    Addon = addonSetup.Result
                });

                AddonListView.ItemsSource = addonList;
                Settings.SetAddonList(addonList);
            }
        }

        private void AddonListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (AddonListView.SelectedItem is AddonEntry entry && entry.Addon is GenericAddon genericAddon)
            {
                var addonSetup = new GenericAddonSetup(genericAddon);
                addonSetup.ShowDialog();

                if (addonSetup.Result != null)
                {
                    var addonList = Settings.GetAddonList();
                    
                    addonList = addonList.Where(x =>
                    {
                        if (x.Addon is RichPresenceAddon)
                            return true;

                        return x.Addon is GenericAddon thisGenericAddon && thisGenericAddon.Path != genericAddon.Path;
                    }).ToList();

                    addonList.Add(new AddonEntry()
                    {
                        IsEnabled = entry.IsEnabled,
                        Addon = addonSetup.Result
                    });

                    AddonListView.ItemsSource = addonList;
                    Settings.SetAddonList(addonList);
                }
            }
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            Settings.SetAddonList((List<AddonEntry>) AddonListView.ItemsSource);
        }

        private void RemoveAddonEntry_OnClick(object sender, RoutedEventArgs e)
        {
            if (AddonListView.SelectedItem is AddonEntry entry && entry.Addon is GenericAddon genericAddon)
            {
                var addonList = Settings.GetAddonList();

                addonList = addonList.Where(x =>
                {
                    if (x.Addon is RichPresenceAddon)
                        return true;

                    return x.Addon is GenericAddon thisGenericAddon && thisGenericAddon.Path != genericAddon.Path;
                }).ToList();

                AddonListView.ItemsSource = addonList;
                Settings.SetAddonList(addonList);
            }
        }
    }
}
