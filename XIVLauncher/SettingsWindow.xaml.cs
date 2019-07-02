using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dalamud.Discord;
using Dalamud.Game.Chat;
using Newtonsoft.Json;
using XIVLauncher.Addon;
using XIVLauncher.Cache;

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
            LanguageComboBox.SelectedIndex = (int) Settings.GetLanguage();
            AddonListView.ItemsSource = Settings.GetAddonList();
            UidCacheCheckBox.IsChecked = Settings.UniqueIdCacheEnabled;

            var featureConfig = Settings.DiscordFeatureConfig;
            ChannelListView.ItemsSource = featureConfig.ChatTypeConfigurations;
            DiscordBotTokenTextBox.Text = featureConfig.Token;

            RmtAdFilterCheckBox.IsChecked = Settings.RmtFilterEnabled;
            EnableHooksCheckBox.IsChecked = Settings.IsInGameAddonEnabled();

            VersionLabel.Text += " - v" + Util.GetAssemblyVersion() + " - " + Util.GetGitHash();
        }

        private void SettingsWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Settings.SetGamePath(GamePathEntry.Text);
            Settings.SetDx11(Dx11RadioButton.IsChecked == true);
            Settings.SetLanguage((ClientLanguage) LanguageComboBox.SelectedIndex);
            Settings.SetAddonList((List<AddonEntry>) AddonListView.ItemsSource);
            Settings.UniqueIdCacheEnabled = UidCacheCheckBox.IsChecked == true;

            Settings.RmtFilterEnabled = RmtAdFilterCheckBox.IsChecked == true;

            Settings.SetInGameAddonEnabled(EnableHooksCheckBox.IsChecked == true);

            var featureConfig = Settings.DiscordFeatureConfig;
            featureConfig.Token = DiscordBotTokenTextBox.Text;
            Settings.DiscordFeatureConfig = featureConfig;

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

            if (!(AddonListView.SelectedItem is AddonEntry entry))
                return;

            if (entry.Addon is RichPresenceAddon)
            {
                MessageBox.Show("This addon shows your character information in your discord profile.",
                    "Addon information", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            if (entry.Addon is HooksAddon)
            {
                MessageBox.Show("This addon facilitates XIVLauncher in-game features like chat filtering.",
                    "Addon information", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            if (entry.Addon is GenericAddon genericAddon)
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

                        if (x.Addon is HooksAddon)
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

                    if (x.Addon is HooksAddon)
                        return true;

                    return x.Addon is GenericAddon thisGenericAddon && thisGenericAddon.Path != genericAddon.Path;
                }).ToList();

                AddonListView.ItemsSource = addonList;
                Settings.SetAddonList(addonList);
            }
        }

        private void ResetCacheButton_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.UniqueIdCache = new List<UniqueIdCacheEntry>();
            Settings.Save();
            MessageBox.Show("Reset. Please restart the app.");

            Environment.Exit(0);
        }

        private void OpenWebhookGuideLabel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            Process.Start("https://github.com/goaaats/FFXIVQuickLauncher/wiki/How-to-set-up-a-discord-bot");
        }

        private void DiscordButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/3NMcUV5");
        }

        private void RemoveChatConfigEntry_OnClick(object sender, RoutedEventArgs e)
        {
            var featureConfig = Settings.DiscordFeatureConfig;
                    
            featureConfig.ChatTypeConfigurations.RemoveAt(ChannelListView.SelectedIndex);

            ChannelListView.ItemsSource = featureConfig.ChatTypeConfigurations;
            Settings.DiscordFeatureConfig = featureConfig;
        }

        private void ChannelListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (!(ChannelListView.SelectedItem is ChatTypeConfiguration configEntry))
                return;

            var channelSetup = new ChatChannelSetup(configEntry);
            channelSetup.ShowDialog();

            if (channelSetup.Result == null)
                return;

            var featureConfig = Settings.DiscordFeatureConfig;
                    
            //featureConfig.ChatTypeConfigurations = featureConfig.ChatTypeConfigurations.Where(x => !x.CompareEx(configEntry)).ToList();
            featureConfig.ChatTypeConfigurations.RemoveAt(ChannelListView.SelectedIndex);
            featureConfig.ChatTypeConfigurations.Add(channelSetup.Result);

            ChannelListView.ItemsSource = featureConfig.ChatTypeConfigurations;
            Settings.DiscordFeatureConfig = featureConfig;
        }

        private void AddChannelConfig_OnClick(object sender, RoutedEventArgs e)
        {
            var channelSetup = new ChatChannelSetup();
            channelSetup.ShowDialog();

            if (channelSetup.Result == null) 
                return;

            var featureConfig = Settings.DiscordFeatureConfig;
            featureConfig.ChatTypeConfigurations.Add(channelSetup.Result);
            ChannelListView.ItemsSource = featureConfig.ChatTypeConfigurations;
            Settings.DiscordFeatureConfig = featureConfig;
        }

        private void SetDutyFinderNotificationChannel_OnClick(object sender, RoutedEventArgs e)
        {
            var featureConfig = Settings.DiscordFeatureConfig;

            var channelConfig = featureConfig.CfNotificationChannel ?? new ChannelConfiguration();

            var channelSetup = new ChatChannelSetup(channelConfig);
            channelSetup.ShowDialog();

            if (channelSetup.Result == null) 
                return;

            featureConfig.CfNotificationChannel = channelSetup.Result.Channel;
            Settings.DiscordFeatureConfig = featureConfig;
        }

        private void SetFateNotificationChannel_OnClick(object sender, RoutedEventArgs e)
        {
            var featureConfig = Settings.DiscordFeatureConfig;

            var channelConfig = featureConfig.FateNotificationChannel ?? new ChannelConfiguration();

            var channelSetup = new ChatChannelSetup(channelConfig);
            channelSetup.ShowDialog();

            if (channelSetup.Result == null) 
                return;

            featureConfig.FateNotificationChannel = channelSetup.Result.Channel;
            Settings.DiscordFeatureConfig = featureConfig;
        }

        private void SetRetainerNotificationChannel_OnClick(object sender, RoutedEventArgs e)
        {
            var featureConfig = Settings.DiscordFeatureConfig;

            var channelConfig = featureConfig.RetainerNotificationChannel ?? new ChannelConfiguration();

            var channelSetup = new ChatChannelSetup(channelConfig);
            channelSetup.ShowDialog();

            if (channelSetup.Result == null) 
                return;

            featureConfig.RetainerNotificationChannel = channelSetup.Result.Channel;
            Settings.DiscordFeatureConfig = featureConfig;
        }

        private void RunIntegrityCheck_OnClick(object s, RoutedEventArgs e)
        {
            var window = new IntegrityCheckProgressWindow();
            var progress = new Progress<IntegrityCheck.IntegrityCheckProgress>();
            progress.ProgressChanged += (sender, checkProgress) => window.UpdateProgress(checkProgress);

            Task.Run(async () => await IntegrityCheck.CompareIntegrityAsync(progress)).ContinueWith(task =>
            {
                window.Dispatcher.Invoke(() => window.Close());

                switch (task.Result.compareResult)
                {
                    case IntegrityCheck.CompareResult.NoServer:
                        MessageBox.Show("There is no reference report yet for this game version. Please try again later.", "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        return;

                    case IntegrityCheck.CompareResult.Invalid:
                    {
                        File.WriteAllText("integrityreport.txt", task.Result.report);
                        var result = MessageBox.Show(
                            $"Some game files seem to be modified or corrupted. Please check the \"integrityreport.txt\" file in the XIVLauncher folder for more information.\n\nDo you want to reset the game to the last patch? This will allow you to patch it again, likely fixing the issues you are encountering.", "XIVLauncher", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                        if (result == MessageBoxResult.Yes)
                        {
                            var verFile = Path.Combine(Settings.GetGamePath(), "game", "ffxivgame.ver");

                            File.Delete(verFile);
                            File.WriteAllText(verFile, task.Result.remoteIntegrity.LastGameVersion);

                            Process.Start(System.IO.Path.Combine(GamePathEntry.Text, "boot", "ffxivboot.exe"));
                            Environment.Exit(0);
                        }

                        break;
                    }

                    case IntegrityCheck.CompareResult.Valid:
                        MessageBox.Show("Your game install seems to be valid.", "XIVLauncher", MessageBoxButton.OK,
                            MessageBoxImage.Asterisk);
                        break;
                }
            });

            window.ShowDialog();
        }
    }
}
