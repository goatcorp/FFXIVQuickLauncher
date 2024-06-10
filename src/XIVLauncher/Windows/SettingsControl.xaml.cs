using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CheapLoc;
using MaterialDesignThemes.Wpf.Transitions;
using Serilog;
using XIVLauncher.Common.Game;
using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Addon.Implementations;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Util;
using XIVLauncher.Support;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl
    {
        public event EventHandler SettingsDismissed;
        public event EventHandler CloseMainWindowGracefully;

        private SettingsControlViewModel ViewModel => DataContext as SettingsControlViewModel;

        private const int BYTES_TO_MB = 1048576;

        private bool _hasTriggeredLogo = false;

        public SettingsControl()
        {
            InitializeComponent();

            DiscordButton.Click += SupportLinks.OpenDiscord;
            FaqButton.Click += SupportLinks.OpenFaq;
            DataContext = new SettingsControlViewModel();

            ReloadSettings();
        }

        public void ReloadSettings()
        {
            if (App.Settings.GamePath != null)
                ViewModel.GamePath = App.Settings.GamePath.FullName;

            if (App.Settings.PatchPath != null)
                ViewModel.PatchPath = App.Settings.PatchPath.FullName;

            LanguageComboBox.SelectedIndex = (int) App.Settings.Language.GetValueOrDefault(ClientLanguage.English);
            LauncherLanguageComboBox.SelectedIndex = (int) App.Settings.LauncherLanguage.GetValueOrDefault(LauncherLanguage.English);
            LauncherLanguageNoticeTextBlock.Visibility = Visibility.Hidden;
            AddonListView.ItemsSource = App.Settings.AddonList ??= new List<AddonEntry>();
            AskBeforePatchingCheckBox.IsChecked = App.Settings.AskBeforePatchInstall;
            KeepPatchesCheckBox.IsChecked = App.Settings.KeepPatches;
            PatchAcquisitionComboBox.SelectedIndex = (int) App.Settings.PatchAcquisitionMethod.GetValueOrDefault(AcquisitionMethod.Aria);
            AutoStartSteamCheckBox.IsChecked = App.Settings.AutoStartSteam;

            InjectionDelayUpDown.Value = App.Settings.DalamudInjectionDelayMs;

            if (App.Settings.InGameAddonLoadMethod == DalamudLoadMethod.DllInject)
                DllInjectDalamudLoadMethodRadioButton.IsChecked = true;
            else
                EntryPointDalamudLoadMethodRadioButton.IsChecked = true;

            // Prevent raising events...
            this.EnableHooksCheckBox.Checked -= this.EnableHooksCheckBox_OnChecked;
            EnableHooksCheckBox.IsChecked = App.Settings.InGameAddonEnabled;
            this.EnableHooksCheckBox.Checked += this.EnableHooksCheckBox_OnChecked;

            OtpServerCheckBox.IsChecked = App.Settings.OtpServerEnabled;

            LaunchArgsTextBox.Text = App.Settings.AdditionalLaunchArgs;

            DpiAwarenessComboBox.SelectedIndex = (int) App.Settings.DpiAwareness.GetValueOrDefault(DpiAwareness.Unaware);

            VersionLabel.Text += " - v" + AppUtil.GetAssemblyVersion() + " - " + AppUtil.GetGitHash() + " - " + Environment.Version;

            var val = (decimal) App.Settings.SpeedLimitBytes / BYTES_TO_MB;

            SpeedLimiterUpDown.Value = val;

            IsFreeTrialCheckbox.IsChecked = App.Settings.IsFt;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.GamePath == ViewModel.PatchPath)
            {
                CustomMessageBox.Show(Loc.Localize("SettingsGamePatchPathError", "Game and patch download paths cannot be the same.\nPlease make sure to choose distinct game and patch download paths."), "XIVLauncher Error", MessageBoxButton.OK,
                    MessageBoxImage.Error, parentWindow: Window.GetWindow(this));
                return;
            }

            App.Settings.GamePath = !string.IsNullOrEmpty(ViewModel.GamePath) ? new DirectoryInfo(ViewModel.GamePath) : null;
            App.Settings.PatchPath = !string.IsNullOrEmpty(ViewModel.PatchPath) ? new DirectoryInfo(ViewModel.PatchPath) : null;

            App.Settings.Language = (ClientLanguage)LanguageComboBox.SelectedIndex;
            // Keep the notice visible if LauncherLanguage has changed
            if (App.Settings.LauncherLanguage == (LauncherLanguage)LauncherLanguageComboBox.SelectedIndex)
                LauncherLanguageNoticeTextBlock.Visibility = Visibility.Hidden;
            App.Settings.LauncherLanguage = (LauncherLanguage)LauncherLanguageComboBox.SelectedIndex;

            App.Settings.AddonList = (List<AddonEntry>)AddonListView.ItemsSource;
            App.Settings.AskBeforePatchInstall = AskBeforePatchingCheckBox.IsChecked == true;
            App.Settings.KeepPatches = KeepPatchesCheckBox.IsChecked == true;
            App.Settings.PatchAcquisitionMethod = (AcquisitionMethod) PatchAcquisitionComboBox.SelectedIndex;
            App.Settings.AutoStartSteam = AutoStartSteamCheckBox.IsChecked == true;

            App.Settings.InGameAddonEnabled = EnableHooksCheckBox.IsChecked == true;

            if (InjectionDelayUpDown.Value.HasValue)
                App.Settings.DalamudInjectionDelayMs = InjectionDelayUpDown.Value.Value;

            if (DllInjectDalamudLoadMethodRadioButton.IsChecked == true)
                App.Settings.InGameAddonLoadMethod = DalamudLoadMethod.DllInject;
            else
                App.Settings.InGameAddonLoadMethod = DalamudLoadMethod.EntryPoint;

            App.Settings.OtpServerEnabled = OtpServerCheckBox.IsChecked == true;

            App.Settings.AdditionalLaunchArgs = LaunchArgsTextBox.Text;

            App.Settings.DpiAwareness = (DpiAwareness) DpiAwarenessComboBox.SelectedIndex;

            SettingsDismissed?.Invoke(this, null);

            App.Settings.SpeedLimitBytes = (long) (SpeedLimiterUpDown.Value * BYTES_TO_MB);

            App.Settings.IsFt = this.IsFreeTrialCheckbox.IsChecked == true;

            Transitioner.MoveNextCommand.Execute(null, null);
        }

        private void GitHubButton_OnClick(object sender, RoutedEventArgs e)
        {
            PlatformHelpers.OpenBrowser("https://github.com/goaaats/FFXIVQuickLauncher");
        }

        private void BackupToolButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(Path.Combine(ViewModel.GamePath, "boot", "ffxivconfig64.exe"));
        }

        private void OriginalLauncherButton_OnClick(object sender, RoutedEventArgs e)
        {
            var isSteam = CustomMessageBox.Builder
                                          .NewFrom(Loc.Localize("LaunchAsSteam", "Launch as a steam user?"))
                                          .WithButtons(MessageBoxButton.YesNo)
                                          .WithImage(MessageBoxImage.Question)
                                          .WithParentWindow(Window.GetWindow(this))
                                          .Show() == MessageBoxResult.Yes;

            GameHelpers.StartOfficialLauncher(App.Settings.GamePath, isSteam, App.Settings.IsFt.GetValueOrDefault(false));
        }

        // All of the list handling is very dirty - but i guess it works

        private void AddAddon_OnClick(object sender, RoutedEventArgs e)
        {
            var addonSetup = new GenericAddonSetupWindow();
            addonSetup.ShowDialog();

            if (addonSetup.Result != null && !string.IsNullOrEmpty(addonSetup.Result.Path)) {
                var addonList = App.Settings.AddonList;

                addonList.Add(new AddonEntry
                {
                    IsEnabled = true,
                    Addon = addonSetup.Result
                });

                App.Settings.AddonList = addonList;

                AddonListView.ItemsSource = App.Settings.AddonList;
            }
        }

        private void AddonListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (!(AddonListView.SelectedItem is AddonEntry entry))
                return;

            if (entry.Addon is GenericAddon genericAddon)
            {
                var selectedIndex = AddonListView.SelectedIndex;
                var addonSetup = new GenericAddonSetupWindow(genericAddon);
                addonSetup.ShowDialog();

                if (addonSetup.Result != null)
                {
                    var addonList = App.Settings.AddonList;
                    addonList.RemoveAt(selectedIndex);
                    addonList.Insert(selectedIndex, new AddonEntry
                    {
                        IsEnabled = entry.IsEnabled,
                        Addon = addonSetup.Result
                    });

                    App.Settings.AddonList = addonList;

                    AddonListView.ItemsSource = App.Settings.AddonList;
                }
            }
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            App.Settings.AddonList = (List<AddonEntry>) AddonListView.ItemsSource;
        }

        private void RemoveAddonEntry_OnClick(object sender, RoutedEventArgs e)
        {
            if (AddonListView.SelectedItem is AddonEntry)
            {
                var addonList = App.Settings.AddonList;
                addonList.RemoveAt(this.AddonListView.SelectedIndex);

                App.Settings.AddonList = addonList;

                AddonListView.ItemsSource = App.Settings.AddonList;
            }
        }

        private void RunIntegrityCheck_OnClick(object s, RoutedEventArgs e)
        {
            var window = new IntegrityCheckProgressWindow();
            var progress = new Progress<IntegrityCheck.IntegrityCheckProgress>();
            progress.ProgressChanged += (sender, checkProgress) => window.UpdateProgress(checkProgress);

            var gamePath = new DirectoryInfo(ViewModel.GamePath);

            if (Repository.Ffxiv.IsBaseVer(gamePath))
            {
                CustomMessageBox.Show(Loc.Localize("IntegrityCheckBase", "The game is not installed to the path you specified.\nPlease install the game before running an integrity check."), "XIVLauncher", parentWindow: Window.GetWindow(this));
                return;
            }

            Task.Run(async () => await IntegrityCheck.CompareIntegrityAsync(progress, gamePath)).ContinueWith(task =>
            {
                window.Dispatcher.Invoke(() => window.Close());

                string saveIntegrityPath = Path.Combine(Paths.RoamingPath, "integrityreport.txt");
#if DEBUG
                Log.Information("Saving integrity to " + saveIntegrityPath);
#endif
                File.WriteAllText(saveIntegrityPath, task.Result.report);

                this.Dispatcher.Invoke(() =>
                {
                    switch (task.Result.compareResult)
                    {
                        case IntegrityCheck.CompareResult.ReferenceNotFound:
                            CustomMessageBox.Show(Loc.Localize("IntegrityCheckImpossible",
                                    "There is no reference report yet for this game version. Please try again later."),
                                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk, parentWindow: Window.GetWindow(this));
                            return;

                        case IntegrityCheck.CompareResult.ReferenceFetchFailure:
                            CustomMessageBox.Show(Loc.Localize("IntegrityCheckNetworkError",
                                    "Failed to download reference files for checking integrity. Check your internet connection and try again."),
                                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: Window.GetWindow(this));
                            return;

                        case IntegrityCheck.CompareResult.Invalid:
                            CustomMessageBox.Show(Loc.Localize("IntegrityCheckFailed",
                                    "Some game files seem to be modified or corrupted. \n\nIf you use TexTools mods, this is an expected result.\n\nIf you do not use mods, right click the \"Login\" button on the XIVLauncher start page and choose \"Repair game\"."),
                                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation, showReportLinks: true, parentWindow: Window.GetWindow(this));
                            break;

                        case IntegrityCheck.CompareResult.Valid:
                            CustomMessageBox.Show(Loc.Localize("IntegrityCheckValid", "Your game install seems to be valid."), "XIVLauncher", MessageBoxButton.OK,
                                MessageBoxImage.Asterisk, parentWindow: Window.GetWindow(this));
                            break;
                    }
                });
            });

            window.ShowDialog();
        }

        private void LauncherLanguageCombo_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (LauncherLanguageNoticeTextBlock != null)
            {
                LauncherLanguageNoticeTextBlock.Visibility = Visibility.Visible;
            }
        }

        private void EnableHooksCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(ViewModel.GamePath) && GameHelpers.IsValidGamePath(ViewModel.GamePath) && !DalamudLauncher.CanRunDalamud(new DirectoryInfo(ViewModel.GamePath)))
                {
                    CustomMessageBox.Show(
                        Loc.Localize("DalamudIncompatible", "Dalamud was not yet updated for your current game version.\nThis is common after patches, so please be patient or ask on the Discord for a status update!"),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk, parentWindow: Window.GetWindow(this));
                }
            }
            catch (Exception exc)
            {
                CustomMessageBox.Show(Loc.Localize("DalamudCompatCheckFailed",
                    "Could not contact the server to get the current compatible game version for Dalamud. This might mean that your .NET installation is too old.\nPlease check the Discord for more information."), "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Hand, parentWindow: Window.GetWindow(this));

                Log.Error(exc, "Couldn't check dalamud compatibility.");
            }
        }

        private void PluginsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var pluginsPath = Path.Combine(Paths.RoamingPath, "installedPlugins");

            try
            {
                Directory.CreateDirectory(pluginsPath);
                Process.Start(pluginsPath);
            }
            catch (Exception ex)
            {
                var error = $"Could not open the plugins folder! {pluginsPath}";
                CustomMessageBox.Show(error,
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: Window.GetWindow(this));
                Log.Error(ex, error);
            }
        }

        private void OpenI18nLabel_OnClick(object sender, MouseButtonEventArgs e)
        {
            PlatformHelpers.OpenBrowser("https://crowdin.com/project/ffxivquicklauncher");
        }

        private void GamePathEntry_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            var isBootOrGame = false;
            var mightBeNonInternationalVersion = false;

            try
            {
                isBootOrGame = !GameHelpers.LetChoosePath(ViewModel.GamePath);
                mightBeNonInternationalVersion = GameHelpers.CanMightNotBeInternationalClient(ViewModel.GamePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not check game path");
            }

            if (isBootOrGame)
            {
                GamePathSafeguardText.Text = ViewModel.GamePathSafeguardLoc;
                GamePathSafeguardText.Visibility = Visibility.Visible;
            }
            else if (mightBeNonInternationalVersion)
            {
                GamePathSafeguardText.Text = ViewModel.GamePathSafeguardRegionLoc;
                GamePathSafeguardText.Visibility = Visibility.Visible;
            }
            else
            {
                GamePathSafeguardText.Visibility = Visibility.Collapsed;
            }
        }

        private void LicenseText_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(Path.Combine(Paths.ResourcesPath, "LICENSE.txt"));
        }

        private void Logo_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
#if DEBUG
            var result = MessageBox.Show("Yes: FTS\nNo: Save troubleshooting\nCancel: Cancel", "XIVLauncher Expert Debugging Interface", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    var fts = new FirstTimeSetup();
                    fts.ShowDialog();

                    Log.Debug($"WasCompleted: {fts.WasCompleted}");

                    this.ReloadSettings();
                    break;
                case MessageBoxResult.No:
                    MessageBox.Show(PackGenerator.SavePack());
                    break;
                case MessageBoxResult.Cancel:
                    return;
            }
#else
            if (_hasTriggeredLogo)
                return;

            Process.Start("explorer.exe", $"/select, \"{PackGenerator.SavePack()}\"");
            _hasTriggeredLogo = true;
#endif
        }

        private void VersionLabel_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            var cw = new ChangelogWindow(EnvironmentSettings.IsPreRelease);
            cw.UpdateVersion(AppUtil.GetAssemblyVersion());
            cw.ShowDialog();
        }

        private void LearnMoreButton_OnClick(object sender, RoutedEventArgs e)
        {
            PlatformHelpers.OpenBrowser("https://goatcorp.github.io/faq/mobile_otp");
        }

        private void IsFreeTrialCheckbox_OnClick(object sender, RoutedEventArgs e)
        {
            if (App.Steam.AsyncStartTask != null)
            {
                CustomMessageBox.Show(Loc.Localize("SteamFtToggleAutoStartWarning", "To apply this setting, XIVLauncher needs to restart.\nPlease reopen XIVLauncher."),
                    "XIVLauncher", image: MessageBoxImage.Information, showDiscordLink: false, showHelpLinks: false);
                App.Settings.IsFt = IsFreeTrialCheckbox.IsChecked == true;
                CloseMainWindowGracefully?.Invoke(this, null);
            }
        }

        private void OpenAdvancedSettings_OnClick(object sender, RoutedEventArgs e)
        {
            var asw = new AdvancedSettingsWindow();
            asw.ShowDialog();
        }
    }
}
