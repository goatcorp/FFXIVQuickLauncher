using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CheapLoc;
using Serilog;
using XIVLauncher.Addon;
using XIVLauncher.Cache;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;
using XIVLauncher.Settings;
using Newtonsoft.Json.Linq;
using XIVLauncher.Game.Patch.Acquisition;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl
    {
        public event EventHandler SettingsDismissed;

        private SettingsControlViewModel ViewModel => DataContext as SettingsControlViewModel;

        private const int BYTES_TO_MB = 1048576;

        public SettingsControl()
        {
            InitializeComponent();

            DiscordButton.Click += Util.OpenDiscord;
            FaqButton.Click += Util.OpenFaq;
            DataContext = new SettingsControlViewModel();
            
            ReloadSettings();
        }

        public void ReloadSettings()
        {
            if (App.Settings.GamePath != null)
                ViewModel.GamePath = App.Settings.GamePath.FullName;

            if (App.Settings.PatchPath != null)
                ViewModel.PatchPath = App.Settings.PatchPath.FullName;

            if (App.Settings.IsDx11)
                Dx11RadioButton.IsChecked = true;
            else
            {
                Dx9RadioButton.IsChecked = true;
                Dx9DisclaimerTextBlock.Visibility = Visibility.Visible;
            }

            LanguageComboBox.SelectedIndex = (int) App.Settings.Language.GetValueOrDefault(ClientLanguage.English);
            LauncherLanguageComboBox.SelectedIndex = (int) App.Settings.LauncherLanguage.GetValueOrDefault(LauncherLanguage.English);
            LauncherLanguageNoticeTextBlock.Visibility = Visibility.Hidden;
            AddonListView.ItemsSource = App.Settings.AddonList ??= new List<AddonEntry>();
            UidCacheCheckBox.IsChecked = App.Settings.UniqueIdCacheEnabled;
            EncryptedArgumentsCheckbox.IsChecked = App.Settings.EncryptArguments;
            AskBeforePatchingCheckBox.IsChecked = App.Settings.AskBeforePatchInstall;
            KeepPatchesCheckBox.IsChecked = App.Settings.KeepPatches;
            PatchAcquisitionComboBox.SelectedIndex = (int) App.Settings.PatchAcquisitionMethod.GetValueOrDefault(AcquisitionMethod.Aria);

            ReloadPluginList();

            InjectionDelayUpDown.Value = App.Settings.DalamudInjectionDelayMs;

            // Prevent raising events...
            this.EnableHooksCheckBox.Checked -= this.EnableHooksCheckBox_OnChecked;
            EnableHooksCheckBox.IsChecked = App.Settings.InGameAddonEnabled;
            this.EnableHooksCheckBox.Checked += this.EnableHooksCheckBox_OnChecked;

            SteamIntegrationCheckBox.IsChecked = App.Settings.SteamIntegrationEnabled;
            OtpServerCheckBox.IsChecked = App.Settings.OtpServerEnabled;

            // Get old setting if there is one
            if (App.Settings.OptOutMbCollection == null)
                App.Settings.OptOutMbCollection = DalamudSettings.GetSettings().OptOutMbCollection.GetValueOrDefault(false);
            MbUploadOptOutCheckBox.IsChecked = App.Settings.OptOutMbCollection;

            LaunchArgsTextBox.Text = App.Settings.AdditionalLaunchArgs;

            VersionLabel.Text += " - v" + Util.GetAssemblyVersion() + " - " + Util.GetGitHash() + " - " + Environment.Version;

            var val = (decimal) App.Settings.SpeedLimitBytes / BYTES_TO_MB;

            SpeedLimiterUpDown.Value = val;
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.GamePath = !string.IsNullOrEmpty(ViewModel.GamePath) ? new DirectoryInfo(ViewModel.GamePath) : null;
            App.Settings.PatchPath = !string.IsNullOrEmpty(ViewModel.PatchPath) ? new DirectoryInfo(ViewModel.PatchPath) : null;
            App.Settings.IsDx11 = Dx11RadioButton.IsChecked == true;

            App.Settings.Language = (ClientLanguage)LanguageComboBox.SelectedIndex;
            // Keep the notice visible if LauncherLanguage has changed
            if (App.Settings.LauncherLanguage == (LauncherLanguage)LauncherLanguageComboBox.SelectedIndex)
                LauncherLanguageNoticeTextBlock.Visibility = Visibility.Hidden;
            App.Settings.LauncherLanguage = (LauncherLanguage)LauncherLanguageComboBox.SelectedIndex;

            App.Settings.AddonList = (List<AddonEntry>)AddonListView.ItemsSource;
            App.Settings.UniqueIdCacheEnabled = UidCacheCheckBox.IsChecked == true;
            App.Settings.EncryptArguments = EncryptedArgumentsCheckbox.IsChecked == true;
            App.Settings.AskBeforePatchInstall = AskBeforePatchingCheckBox.IsChecked == true;
            App.Settings.KeepPatches = KeepPatchesCheckBox.IsChecked == true;
            App.Settings.PatchAcquisitionMethod = (AcquisitionMethod) PatchAcquisitionComboBox.SelectedIndex;

            App.Settings.InGameAddonEnabled = EnableHooksCheckBox.IsChecked == true;

            if (InjectionDelayUpDown.Value.HasValue)
                App.Settings.DalamudInjectionDelayMs = InjectionDelayUpDown.Value.Value;

            App.Settings.SteamIntegrationEnabled = SteamIntegrationCheckBox.IsChecked == true;
            App.Settings.OtpServerEnabled = OtpServerCheckBox.IsChecked == true;

            App.Settings.OptOutMbCollection = MbUploadOptOutCheckBox.IsChecked == true;

            App.Settings.AdditionalLaunchArgs = LaunchArgsTextBox.Text;

            SettingsDismissed?.Invoke(this, null);

            App.Settings.SpeedLimitBytes = (long) (SpeedLimiterUpDown.Value * BYTES_TO_MB);
        }

        private void GitHubButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/goaaats/FFXIVQuickLauncher");
        }

        private void BackupToolButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(Path.Combine(ViewModel.GamePath, "boot", "ffxivconfig.exe"));
        }

        private void OriginalLauncherButton_OnClick(object sender, RoutedEventArgs e)
        {
            var isSteam =
                MessageBox.Show(Loc.Localize("LaunchAsSteam", "Launch as a steam user?"), "XIVLauncher", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes;
            Util.StartOfficialLauncher(App.Settings.GamePath, isSteam);
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
                var addonSetup = new GenericAddonSetupWindow(genericAddon);
                addonSetup.ShowDialog();

                if (addonSetup.Result != null)
                {
                    App.Settings.AddonList = App.Settings.AddonList.Where(x => x.Addon is GenericAddon thisGenericAddon && thisGenericAddon.Path != genericAddon.Path).ToList();

                    var addonList = App.Settings.AddonList;

                    addonList.Add(new AddonEntry
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
            if (AddonListView.SelectedItem is AddonEntry entry && entry.Addon is GenericAddon genericAddon)
            {
                App.Settings.AddonList = App.Settings.AddonList.Where(x => x.Addon is GenericAddon thisGenericAddon && thisGenericAddon.Path != genericAddon.Path).ToList();

                AddonListView.ItemsSource = App.Settings.AddonList;
            }
        }

        private void ResetCacheButton_OnClick(object sender, RoutedEventArgs e)
        {
            UniqueIdCache.Instance.Reset();
        }

        private void RunIntegrityCheck_OnClick(object s, RoutedEventArgs e)
        {
            var window = new IntegrityCheckProgressWindow();
            var progress = new Progress<IntegrityCheck.IntegrityCheckProgress>();
            progress.ProgressChanged += (sender, checkProgress) => window.UpdateProgress(checkProgress);

            var gamePath = new DirectoryInfo(ViewModel.GamePath);

            Task.Run(async () => await IntegrityCheck.CompareIntegrityAsync(progress, gamePath)).ContinueWith(task =>
            {
                window.Dispatcher.Invoke(() => window.Close());

                string saveIntegrityPath = Path.Combine(Paths.RoamingPath, "integrityreport.txt");
#if DEBUG
                Log.Information("Saving integrity to " + saveIntegrityPath);
#endif
                File.WriteAllText(saveIntegrityPath, task.Result.report);

                switch (task.Result.compareResult)
                {
                    case IntegrityCheck.CompareResult.NoServer:
                        CustomMessageBox.Show(Loc.Localize("IntegrityCheckImpossible",
                            "There is no reference report yet for this game version. Please try again later."),
                            "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                        return;

                    case IntegrityCheck.CompareResult.Invalid:
                        CustomMessageBox.Show(Loc.Localize("IntegrityCheckFailed",
                            "Some game files seem to be modified or corrupted. Please check the \"integrityreport.txt\" file in the XIVLauncher folder for more information.\n\nIf you use TexTools mods, this is an expected result.\n\nIf you do not use mods, please reinstall your game."),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    break;

                    case IntegrityCheck.CompareResult.Valid:
                        CustomMessageBox.Show(Loc.Localize("IntegrityCheckValid", "Your game install seems to be valid."), "XIVLauncher", MessageBoxButton.OK,
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
                if (!string.IsNullOrEmpty(ViewModel.GamePath) && Util.IsValidFfxivPath(ViewModel.GamePath) && !DalamudLauncher.CanRunDalamud(new DirectoryInfo(ViewModel.GamePath)))
                    CustomMessageBox.Show(
                        Loc.Localize("DalamudIncompatible", "The XIVLauncher in-game addon was not yet updated for your current FFXIV version.\nThis is common after patches, so please be patient or ask on the Discord for a status update!"),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch(Exception exc)
            {
                CustomMessageBox.Show(Loc.Localize("DalamudCompatCheckFailed",
                    "Could not contact the server to get the current compatible FFXIV version for the in-game addon. This might mean that your .NET installation is too old.\nPlease check the Discord for more information."), "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Hand);

                Log.Error(exc, "Couldn't check dalamud compatibility.");
            }
        }

        private string GetSelectedPluginVersionPath()
        {
            string selectedPath = null;

            var definitionFiles = Directory.GetFiles(Path.Combine(Paths.RoamingPath, "installedPlugins"), "*.json", SearchOption.AllDirectories);
            foreach (var path in definitionFiles)
            {
                dynamic definition = JObject.Parse(File.ReadAllText(path));
                try
                {
                    if (PluginListView.SelectedValue.ToString().Contains(definition.Name.Value + " " + definition.AssemblyVersion.Value))
                    {
                        selectedPath = Path.GetDirectoryName(path);
                        break;
                    }
                }
                catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                {
                    //Ignore files that are not config
                }
            }

            return selectedPath;
        }

        private void TogglePlugin_OnClick(object sender, RoutedEventArgs e)
        {
            var pluginVersionPath = GetSelectedPluginVersionPath();

            if (pluginVersionPath == null)
            {
                CustomMessageBox.Show(Loc.Localize("PluginPathNotFound", "Couldn't find plugin directory path."), "XIVLauncher Problem");
                return;
            }

            if (PluginListView.SelectedValue.ToString().Contains("(disabled)")) //If it's disabled...
            {
                if (File.Exists(Path.Combine(pluginVersionPath, ".disabled")))
                {
                    File.Delete(Path.Combine(pluginVersionPath, ".disabled")); //Enable it
                }
            }
            else //If it's enabled...
            {
                if (!File.Exists(Path.Combine(pluginVersionPath, ".disabled")))
                {
                    File.WriteAllText(Path.Combine(pluginVersionPath, ".disabled"), ""); //Disable it
                }
            }

            ReloadPluginList();
        }

        private void DeletePlugin_OnClick(object sender, RoutedEventArgs e)
        {
            if(Util.CheckIsGameOpen())
            {
                CustomMessageBox.Show(Loc.Localize("GameIsOpenPluginLocked", "The game is open, please close it and try again."), "XIVLauncher Problem");
                return;
            }

            var pluginVersionPath = GetSelectedPluginVersionPath();

            if (pluginVersionPath == null)
            {
                CustomMessageBox.Show(Loc.Localize("PluginPathNotFound", "Couldn't find plugin directory path."), "XIVLauncher Problem");
                return;
            }

            var pluginDirectory = Directory.GetParent(pluginVersionPath);

            //Just to be safe
            if (pluginDirectory.Parent.FullName != Path.Combine(Paths.RoamingPath, "installedPlugins"))
            {
                CustomMessageBox.Show(Loc.Localize("PluginPathNotFound", "Couldn't find plugin directory path."), "XIVLauncher Problem");
                return;
            }

            pluginDirectory.Delete(true);

            ReloadPluginList();
        }

        private void ReloadPluginList()
        {
            PluginListView.Items.Clear();

            try
            {
                var pluginsDirectory = new DirectoryInfo(Path.Combine(Paths.RoamingPath, "installedPlugins"));

                if (!pluginsDirectory.Exists)
                    return;

                foreach (var installed in pluginsDirectory.GetDirectories())
                {
                    var versions = installed.GetDirectories();

                    if (versions.Length == 0)
                    {
                        Log.Information("Has no versions: {0}", installed.FullName);
                        continue;
                    }

                    var sortedVersions = versions.OrderBy(dirInfo => {
                        var success = Version.TryParse(dirInfo.Name, out Version version);
                        if (!success) { Log.Debug("Unparseable version: {0}", dirInfo.Name); }
                        return version;
                    }).ToArray();
                    var latest = sortedVersions.Last();

                    var localInfoFile = new FileInfo(Path.Combine(latest.FullName, $"{installed.Name}.json"));

                    if (!localInfoFile.Exists)
                    {
                        Log.Information("Has no definition: {0}", localInfoFile.FullName);
                        continue;
                    }

                    dynamic pluginConfig = JObject.Parse(File.ReadAllText(localInfoFile.FullName));
                    var isDisabled = File.Exists(Path.Combine(latest.FullName, ".disabled"));

                    if (isDisabled)
                    {
                        PluginListView.Items.Add(pluginConfig.Name + " " + pluginConfig.AssemblyVersion +
                                                 Loc.Localize("DisabledPlugin", " (disabled)"));
                    }
                    else
                    {
                        PluginListView.Items.Add(pluginConfig.Name + " " + pluginConfig.AssemblyVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not parse installed in-game plugins.");
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
                MessageBox.Show(error,
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.Error(ex, error);
            }
        }

        private void OpenI18nLabel_OnClick(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://crowdin.com/project/ffxivquicklauncher");
        }

        private void GamePathEntry_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            GamePathSafeguardText.Visibility = !Util.LetChoosePath(ViewModel.GamePath) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LicenseText_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(Path.Combine(Paths.ResourcesPath, "LICENSE.txt"));
        }

        private void Logo_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
#if DEBUG
            var fts = new FirstTimeSetup();
            fts.ShowDialog();

            Log.Debug($"WasCompleted: {fts.WasCompleted}");

            this.ReloadSettings();
#endif
        }

        private void VersionLabel_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            new ChangelogWindow().ShowDialog();
        }
    }
}
