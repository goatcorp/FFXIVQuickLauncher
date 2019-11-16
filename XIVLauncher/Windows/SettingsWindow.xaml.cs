using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dalamud.Discord;
using Serilog;
using XIVLauncher.Addon;
using XIVLauncher.Cache;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : INotifyPropertyChanged
    {
        private string gamePath;

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
            get => gamePath;
            set
            {
                gamePath = value;
                OnPropertyChanged(nameof(GamePath));
                OnPropertyChanged(nameof(IsRunIntegrityCheckPossible));
            }
        }

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;
            GamePath = Settings.GamePath?.FullName;

            if (Settings.IsDX11())
                Dx11RadioButton.IsChecked = true;
            else
            {
                Dx9RadioButton.IsChecked = true;
                Dx9DisclaimerTextBlock.Visibility = Visibility.Visible;
            }

            LanguageComboBox.SelectedIndex = (int) Settings.GetLanguage();
            AddonListView.ItemsSource = Settings.GetAddonList();
            UidCacheCheckBox.IsChecked = Settings.UniqueIdCacheEnabled;

            var featureConfig = Settings.DiscordFeatureConfig;
            ChannelListView.ItemsSource = featureConfig.ChatTypeConfigurations;
            DiscordBotTokenTextBox.Text = featureConfig.Token;
            CheckForDuplicateMessagesCheckBox.IsChecked = featureConfig.CheckForDuplicateMessages;
            ChatDelayTextBox.Text = featureConfig.ChatDelayMs.ToString();
            DisableEmbedsCheckBox.IsChecked = featureConfig.DisableEmbeds;

            RmtAdFilterCheckBox.IsChecked = Settings.RmtFilterEnabled;
            EnableHooksCheckBox.IsChecked = Settings.IsInGameAddonEnabled();

            SteamIntegrationCheckBox.IsChecked = Settings.SteamIntegrationEnabled;

            MbUploadOptOutCheckBox.IsChecked = Settings.OptOutMbUpload;

            //CharacterSyncCheckBox.IsChecked = Settings.CharacterSyncEnabled;

            LaunchArgsTextBox.Text = Settings.AdditionalLaunchArgs;

            VersionLabel.Text += " - v" + Util.GetAssemblyVersion() + " - " + Util.GetGitHash() + " - " + Environment.Version;

            // Gotta do this after setup so we don't fire events yet
            //CharacterSyncCheckBox.Checked += CharacterSyncCheckBox_Checked;

            EnableAstCardStuff.IsChecked =
                Settings.ComboPresets.HasFlag(CustomComboPreset.AstrologianCardsOnDrawFeature);

            EnableHooksCheckBox.Checked += EnableHooksCheckBox_OnChecked;
        }

        private void SettingsWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Settings.GamePath = !string.IsNullOrEmpty(GamePath) ? new DirectoryInfo(GamePath) : null;
            Settings.SetDx11(Dx11RadioButton.IsChecked == true);
            Settings.SetLanguage((ClientLanguage) LanguageComboBox.SelectedIndex);
            Settings.SetAddonList((List<AddonEntry>) AddonListView.ItemsSource);
            Settings.UniqueIdCacheEnabled = UidCacheCheckBox.IsChecked == true;

            Settings.RmtFilterEnabled = RmtAdFilterCheckBox.IsChecked == true;

            Settings.SetInGameAddonEnabled(EnableHooksCheckBox.IsChecked == true);

            var featureConfig = Settings.DiscordFeatureConfig;
            featureConfig.Token = DiscordBotTokenTextBox.Text;
            featureConfig.CheckForDuplicateMessages = CheckForDuplicateMessagesCheckBox.IsChecked == true;
            if (int.TryParse(ChatDelayTextBox.Text, out var parsedDelay))
                featureConfig.ChatDelayMs = parsedDelay;
            featureConfig.DisableEmbeds = DisableEmbedsCheckBox.IsChecked == true;
            Settings.DiscordFeatureConfig = featureConfig;

            Settings.SteamIntegrationEnabled = SteamIntegrationCheckBox.IsChecked == true;

            Settings.OptOutMbUpload = MbUploadOptOutCheckBox.IsChecked == true;

            //Settings.CharacterSyncEnabled = CharacterSyncCheckBox.IsChecked == true;

            Settings.AdditionalLaunchArgs = LaunchArgsTextBox.Text;

            if (EnableAstCardStuff.IsChecked == true)
            {
                Settings.ComboPresets |= CustomComboPreset.AstrologianCardsOnDrawFeature;
            }

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
            Process.Start(Path.Combine(GamePath, "boot", "ffxivconfig.exe"));
        }

        private void OriginalLauncherButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(Path.Combine(GamePath, "boot", "ffxivboot.exe"));
        }

        // All of the list handling is very dirty - but i guess it works

        private void AddAddon_OnClick(object sender, RoutedEventArgs e)
        {
            var addonSetup = new GenericAddonSetupWindow();
            addonSetup.ShowDialog();

            if (addonSetup.Result != null && !string.IsNullOrEmpty(addonSetup.Result.Path))
            {
                var addonList = Settings.GetAddonList();

                addonList.Add(new AddonEntry
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

            if (entry.Addon is GenericAddon genericAddon)
            {
                var addonSetup = new GenericAddonSetupWindow(genericAddon);
                addonSetup.ShowDialog();

                if (addonSetup.Result != null)
                {
                    var addonList = Settings.GetAddonList();

                    addonList = addonList.Where(x =>
                    {
                        if (x.Addon is RichPresenceAddon)
                            return true;

                        if (x.Addon is DalamudLauncher)
                            return true;

                        return x.Addon is GenericAddon thisGenericAddon && thisGenericAddon.Path != genericAddon.Path;
                    }).ToList();

                    addonList.Add(new AddonEntry
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

                    if (x.Addon is DalamudLauncher)
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
                        MessageBox.Show(
                            "There is no reference report yet for this game version. Please try again later.",
                            "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        return;

                    case IntegrityCheck.CompareResult.Invalid:
                    {
                        File.WriteAllText("integrityreport.txt", task.Result.report);
                        var result = MessageBox.Show(
                            "Some game files seem to be modified or corrupted. Please check the \"integrityreport.txt\" file in the XIVLauncher folder for more information.\n\nDo you want to reset the game to the last patch? This will allow you to patch it again, likely fixing the issues you are encountering.",
                            "XIVLauncher", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

                        if (result == MessageBoxResult.Yes)
                        {
                            var verFile = Path.Combine(Settings.GamePath.FullName, "game", "ffxivgame.ver");

                            File.Delete(verFile);
                            File.WriteAllText(verFile, task.Result.remoteIntegrity.LastGameVersion);

                            Process.Start(Path.Combine(GamePath, "boot", "ffxivboot.exe"));
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

        private void Dx9RadioButton_OnChecked(object sender, RoutedEventArgs e)
        {
            Dx9DisclaimerTextBlock.Visibility = Visibility.Visible;
        }

        private void Dx9RadioButton_OnUnchecked(object sender, RoutedEventArgs e)
        {
            Dx9DisclaimerTextBlock.Visibility = Visibility.Hidden;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void CharacterSyncCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("ATTENTION!!!\n\n\"Synchronize Character Data\" synchronizes hotbars, HUD and settings of the character you last logged in with to your other characters after closing the game.\nWhen enabling this feature, make sure that you log in with your main character on the first launch of your game.\nClose it immediately after to start syncing files from this character to your other characters.\n\nIf you use another character first, your main character will be overwritten.", "Danger Zone", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ManageCustomCombosButton_OnClick(object sender, RoutedEventArgs e)
        {
            var comboWindow = new CustomComboSetupWindow(Settings.ComboPresets);
            comboWindow.ShowDialog();

            Settings.ComboPresets = comboWindow.EnabledPresets;
        }

        private void EnableHooksCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!DalamudLauncher.CanRunDalamud())
                    MessageBox.Show(
                        $"The XIVLauncher in-game addon was not yet updated for your current FFXIV version.\nThis is common after patches, so please be patient or ask on the discord for a status update!",
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch(Exception exc)
            {
                MessageBox.Show(
                    "Could not contact the server to get the current compatible FFXIV version for the in-game addon. This might mean that your .NET installation is too old.\nPlease check the discord for more information");

                Log.Error(exc, "Couldn't check dalamud compatibility.");
            }
        }
    }
}