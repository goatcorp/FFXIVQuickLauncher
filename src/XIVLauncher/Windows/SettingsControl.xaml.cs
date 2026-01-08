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
using Microsoft.Win32;
using Serilog;
using XIVLauncher.Common.Game;
using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Addon.Implementations;
using XIVLauncher.Common.Dalamud;
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

        private bool _hasTriggeredLogo = false;

        public SettingsControl()
        {
            InitializeComponent();

            DiscordButton.Click += SupportLinks.OpenDiscord;
            FaqButton.Click += SupportLinks.OpenFaq;
            DataContext = new SettingsControlViewModel();

            ReloadSettings();
        }

        private void OnResolvedBranchChanged(DalamudVersionInfo? branch)
        {
            this.Dispatcher.Invoke(() => { this.DalamudBranchTextBlock.Text = branch == null ? "Transmitting..." : $"{branch.DisplayName} ({branch.Track})"; });
        }

        public void SetUpdater(DalamudUpdater updater)
        {
            updater.ResolvedBranchChanged += OnResolvedBranchChanged;
            OnResolvedBranchChanged(updater.ResolvedBranch);
        }

        public void ReloadSettings()
        {
            if (App.Settings.GamePath != null)
                ViewModel.GamePath = App.Settings.GamePath.FullName;

            if (App.Settings.PatchPath != null)
                ViewModel.PatchPath = App.Settings.PatchPath.FullName;

            LanguageComboBox.SelectedIndex = (int) App.Settings.Language.GetValueOrDefault(ClientLanguage.English);
            ViewModel.LauncherLanguage = App.Settings.LauncherLanguage.GetValueOrDefault(LauncherLanguage.English);
            ViewModel.LauncherLanguageNoticeVisiable = Visibility.Hidden;
            AddonListView.ItemsSource = App.Settings.AddonList ??= new List<AddonEntry>();
            AskBeforePatchingCheckBox.IsChecked = App.Settings.AskBeforePatchInstall;
            KeepPatchesCheckBox.IsChecked = App.Settings.KeepPatches;
            AutoStartSteamCheckBox.IsChecked = App.Settings.AutoStartSteam;

            // Prevent raising events...
            this.EnableHooksCheckBox.Checked -= this.EnableHooksCheckBox_OnChecked;
            EnableHooksCheckBox.IsChecked = App.Settings.InGameAddonEnabled;
            this.EnableHooksCheckBox.Checked += this.EnableHooksCheckBox_OnChecked;

            OtpServerCheckBox.IsChecked = App.Settings.OtpServerEnabled;

            OtpAlwaysOnTopCheckBox.IsChecked = App.Settings.OtpAlwaysOnTopEnabled;

            LaunchArgsTextBox.Text = App.Settings.AdditionalLaunchArgs;

            DpiAwarenessComboBox.SelectedIndex = (int) App.Settings.DpiAwareness.GetValueOrDefault(DpiAwareness.Unaware);

            VersionLabel.Text += " - v" + AppUtil.GetAssemblyVersion() + " - " + AppUtil.GetGitHash() + " - " + Environment.Version;

            var val = (decimal) App.Settings.SpeedLimitBytes / MathHelpers.BYTES_TO_MB;

            this.SpeedLimitSpinBox.Value = (double)val;

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
            App.Settings.LauncherLanguage = ViewModel.LauncherLanguage;

            App.Settings.AddonList = (List<AddonEntry>)AddonListView.ItemsSource;
            App.Settings.AskBeforePatchInstall = AskBeforePatchingCheckBox.IsChecked == true;
            App.Settings.KeepPatches = KeepPatchesCheckBox.IsChecked == true;
            App.Settings.AutoStartSteam = AutoStartSteamCheckBox.IsChecked == true;

            App.Settings.InGameAddonEnabled = EnableHooksCheckBox.IsChecked == true;

            App.Settings.OtpServerEnabled = OtpServerCheckBox.IsChecked == true;

            App.Settings.OtpAlwaysOnTopEnabled = OtpAlwaysOnTopCheckBox.IsChecked == true;

            App.Settings.AdditionalLaunchArgs = LaunchArgsTextBox.Text;

            App.Settings.DpiAwareness = (DpiAwareness) DpiAwarenessComboBox.SelectedIndex;

            SettingsDismissed?.Invoke(this, null);

            App.Settings.SpeedLimitBytes = (long)(this.SpeedLimitSpinBox.Value * MathHelpers.BYTES_TO_MB);

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

        private void EnableHooksCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ViewModel.GamePath) || !GameHelpers.PathHasExistingInstall(ViewModel.GamePath))
                return;

            try
            {
                var applicable = App.DalamudUpdater.ReCheckVersion(new DirectoryInfo(ViewModel.GamePath));

                if (!applicable.HasValue)
                {
                    CustomMessageBox.Show(
                        Loc.Localize("DalamudEnsureFail", "Could not determine Dalamud compatibility for the selected game version.\nPlease ensure that the game path is correct and that the game is fully updated."),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk, parentWindow: Window.GetWindow(this));
                }
                else if ((bool)!applicable)
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
                Process.Start("explorer.exe", pluginsPath);
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

        private void OpenDalamudBranchSwitcher_OnClick(object sender, RoutedEventArgs e)
        {
            // TODO: Queue this?
            if (App.DalamudUpdater.State == DalamudUpdater.DownloadState.Running)
            {
                CustomMessageBox.Show(Loc.Localize("DalamudBranchSwitcherBusy", "Cannot switch Dalamud branches while an update is in progress.\nPlease wait a little while before trying again."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Warning, parentWindow: Window.GetWindow(this));
                return;
            }

            var window = new DalamudBranchSwitcherWindow
            {
                Owner = Window.GetWindow(this)
            };

            window.ShowDialog();
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

        public static DirectoryInfo GetGameUserDirectory()
        {
            var argumentPath = App.Settings.AdditionalLaunchArgs;

            if (string.IsNullOrEmpty(argumentPath) || !argumentPath.Contains("UserPath="))
            {
                var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var savedGames = Path.Combine(myDocuments, "My Games");
                var defaultPath = Path.Combine(savedGames, "FINAL FANTASY XIV - A Realm Reborn");

                return new DirectoryInfo(defaultPath);
            }

            var userPath = argumentPath.Split("UserPath=")[1].Trim('"');
            return new DirectoryInfo(userPath);
        }

        private void CreateBackup_OnClick(object sender, RoutedEventArgs e)
        {
            _ = CreateBackupAsync();
        }

        private async Task CreateBackupAsync()
        {
            var parent = Window.GetWindow(this);

            if (GameHelpers.CheckIsGameOpen())
            {
                CustomMessageBox.Show(Loc.Localize("CreateBackupGameOpenError", "Please close the game before creating a backup."), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Multiselect = false,
                Title = "Select a location to save the backup file",
                Filter = $"XIVLauncher Backup File (*{Backup.BackupExtension})|*{Backup.BackupExtension}",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = $"xiv_{DateTime.Now:M_d_yy_HH_MM}" + Backup.BackupExtension
            };

            if (!dlg.ShowDialog(parent).GetValueOrDefault(false))
                return;

            // Disable UI and show spinner
            SetBackupUiBusy(true, isCreating: true);

            try
            {
                var includeUserFiles = this.BackupIncludeGameSettingsCheckBox.IsChecked == true;
                await Task.Run(() => Backup.CreateBackup(
                    new DirectoryInfo(Paths.RoamingPath),
                    includeUserFiles ? GetGameUserDirectory() : null,
                    new FileInfo(dlg.FileName)));

                CustomMessageBox.Show(Loc.Localize("BackupCreateSuccess", "Backup created successfully."), "XIVLauncher", parentWindow: parent);
            }
            catch (BackupFileException ex)
            {
                var msg = Loc.Localize("BackupCreateFailed", "Could not create backup for the file:") + "\n" + ex.FilePath + "\n\n" + ex.Message;
                CustomMessageBox.Show(msg, "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                Log.Error(ex, "CreateBackup failed for file {File}", ex.FilePath);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(Loc.Localize("BackupCreateFailedGeneric", "Failed to create backup."), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                Log.Error(ex, "CreateBackup failed");
            }
            finally
            {
                SetBackupUiBusy(false);
            }
        }

        private void RestoreBackup_OnClick(object sender, RoutedEventArgs e)
        {
            _ = RestoreBackupAsync();
        }

        private async Task RestoreBackupAsync()
        {
            var parent = Window.GetWindow(this);

            if (GameHelpers.CheckIsGameOpen())
            {
                CustomMessageBox.Show(Loc.Localize("RestoreBackupGameOpenError", "Please close the game before restoring a backup."), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Multiselect = false,
                Title = "Select a backup to import",
                Filter = $"XIVLauncher Backup File (*{Backup.BackupExtension})|*{Backup.BackupExtension}",
                ValidateNames = true,
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dlg.ShowDialog(parent) != true)
                return;

            var doRestoreUserFiles = false;

            try
            {
                // check for user files, wrap in Task.Run because it opens the archive
                var hasUser = await Task.Run(() => Backup.BackupHasUserFiles(new FileInfo(dlg.FileName)));

                if (hasUser)
                {
                    var result = CustomMessageBox.Builder
                        .NewFrom(Loc.Localize("BackupRestoreUserFilesPrompt", "This backup contains game and character settings files.\nDo you want to restore them as well?"))
                        .WithButtons(MessageBoxButton.YesNoCancel)
                        .WithImage(MessageBoxImage.Question)
                        .WithParentWindow(parent)
                        .Show();

                    if (result == MessageBoxResult.Cancel)
                        return;

                    doRestoreUserFiles = result == MessageBoxResult.Yes;
                }
            }
            catch (BackupFileException ex)
            {
                CustomMessageBox.Show(Loc.Localize("BackupOpenFailed", "Could not restore file in backup. The file may be unreadable.\n\nFile path:") + ex.FilePath, "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                return;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(Loc.Localize("BackupOpenFailedGeneric", "Failed to open backup file."), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                Log.Error(ex, "Backup open failed");
                return;
            }

            SetBackupUiBusy(true, isCreating: false);

            try
            {
                await Task.Run(() => Backup.RestoreBackup(
                    new DirectoryInfo(Paths.RoamingPath),
                    doRestoreUserFiles ? GetGameUserDirectory() : null,
                    new FileInfo(dlg.FileName)));

                App.SetupSettings();
                this.ReloadSettings();

                CustomMessageBox.Show(Loc.Localize("BackupRestoreSuccess", "Backup restored successfully."), "XIVLauncher", parentWindow: parent);
            }
            catch (BackupFileException ex)
            {
                var msg = Loc.Localize("BackupRestoreFailed", "Could not restore file:") + "\n" + ex.FilePath + "\n\n" + ex.Message;
                CustomMessageBox.Show(msg, "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                Log.Error(ex, "RestoreBackup failed for file {File}", ex.FilePath);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(Loc.Localize("BackupRestoreFailedGeneric", "Failed to restore backup."), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parent);
                Log.Error(ex, "RestoreBackup failed");
            }
            finally
            {
                SetBackupUiBusy(false);
            }
        }

        private void SetBackupUiBusy(bool busy, bool isCreating = false)
        {
            this.Dispatcher.Invoke(() =>
            {
                SettingsTabControl.IsEnabled = !busy;
                AcceptSettingsButton.IsEnabled = !busy;

                if (busy)
                {
                    if (isCreating)
                    {
                        CreateBackupContent.Visibility = Visibility.Collapsed;
                        CreateBackupSpinner.Visibility = Visibility.Visible;
                        CreateBackupButton.IsEnabled = false;
                        RestoreBackupButton.IsEnabled = false;
                    }
                    else
                    {
                        RestoreBackupContent.Visibility = Visibility.Collapsed;
                        RestoreBackupSpinner.Visibility = Visibility.Visible;
                        RestoreBackupButton.IsEnabled = false;
                        CreateBackupButton.IsEnabled = false;
                    }
                }
                else
                {
                    CreateBackupContent.Visibility = Visibility.Visible;
                    CreateBackupSpinner.Visibility = Visibility.Collapsed;
                    CreateBackupButton.IsEnabled = true;

                    RestoreBackupContent.Visibility = Visibility.Visible;
                    RestoreBackupSpinner.Visibility = Visibility.Collapsed;
                    RestoreBackupButton.IsEnabled = true;
                }
            });
        }
    }
}
