using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AdysTech.CredentialManager;
using AutoUpdaterDotNET;
using MaterialDesignThemes.Wpf;
using Serilog;
using XIVLauncher.Accounts;
using XIVLauncher.Addon;
using XIVLauncher.Addon.Implementations;
using XIVLauncher.Cache;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;
using XIVLauncher.Game.Patch;
using Timer = System.Timers.Timer;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Timer _bannerChangeTimer;
        private Headlines _headlines;
        private BitmapImage[] _bannerBitmaps;
        private int _currentBannerIndex;

        private Timer _maintenanceQueueTimer;

        private readonly XivGame _game = new XivGame();

        private AccountManager _accountManager;

        private bool _isLoggingIn;

        public MainWindow(string accountName)
        {
            InitializeComponent();

            Title += " v" + Util.GetAssemblyVersion();

            if (!string.IsNullOrEmpty(accountName))
            {
                Properties.Settings.Default.CurrentAccount = accountName;
            }

#if !DEBUG
            AutoUpdater.ShowSkipButton = false;
            AutoUpdater.ShowRemindLaterButton = false;
            AutoUpdater.Mandatory = true;
            AutoUpdater.UpdateMode = Mode.Forced;

            AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;

            Log.Information("Starting update check.");
            AutoUpdater.Start("https://goaaats.github.io/ffxiv/tools/launcher/update.xml");
#else
            InitializeWindow();
#endif
        }

        private void SetupHeadlines()
        {
            try
            {
                _bannerChangeTimer?.Stop();

                _headlines = Headlines.Get(_game);

                _bannerBitmaps = new BitmapImage[_headlines.Banner.Length];
                for (var i = 0; i < _headlines.Banner.Length; i++)
                {
                    var imageBytes = _game.DownloadAsLauncher(_headlines.Banner[i].LsbBanner.ToString());

                    using (var stream = new MemoryStream(imageBytes))
                    {
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.StreamSource = stream;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();

                        _bannerBitmaps[i] = bitmapImage;
                    }
                }

                Dispatcher.BeginInvoke(new Action(() => { BannerImage.Source = _bannerBitmaps[0]; }));

                _bannerChangeTimer = new Timer {Interval = 5000};

                _bannerChangeTimer.Elapsed += (o, args) =>
                {
                    if (_currentBannerIndex + 1 > _headlines.Banner.Length - 1)
                        _currentBannerIndex = 0;
                    else
                        _currentBannerIndex++;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        BannerImage.Source = _bannerBitmaps[_currentBannerIndex];
                    }));
                };

                _bannerChangeTimer.AutoReset = true;
                _bannerChangeTimer.Start();

                Dispatcher.BeginInvoke(new Action(() => { NewsListView.ItemsSource = _headlines.News; }));
            }
            catch (Exception)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NewsListView.Items.Add(new News {Title = "Could not download news data.", Tag = "DlError"});
                }));
            }
        }

        private void InitializeWindow()
        {
            // Upgrade the stored settings if needed
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Log.Information("Settings upgrade required...");
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            var gateStatus = false;
            try
            {
                gateStatus = _game.GetGateStatus();
            }
            catch
            {
                // ignored
            }

            if (!gateStatus) WorldStatusPackIcon.Foreground = new SolidColorBrush(Color.FromRgb(242, 24, 24));

            var version = Util.GetAssemblyVersion();
            if (Properties.Settings.Default.LastVersion != version)
            {
                new ChangelogWindow().ShowDialog();

                Properties.Settings.Default.LastVersion = version;
                Settings.UniqueIdCache = new List<UniqueIdCacheEntry>();

                if (version == "3.4.0.0")
                {
                    var savedCredentials = CredentialManager.GetCredentials("FINAL FANTASY XIV");

                    if (savedCredentials != null)
                    {
                        _accountManager.AddAccount(new XivAccount(savedCredentials.UserName)
                        {
                            Password = savedCredentials.Password,
                            SavePassword = true,
                            UseOtp = Settings.NeedsOtp(),
                            UseSteamServiceAccount = Settings.SteamIntegrationEnabled
                        });

                        Properties.Settings.Default.CurrentAccount = $"{savedCredentials.UserName}-{Settings.NeedsOtp()}-{Settings.SteamIntegrationEnabled}";;
                    }
                }

                Properties.Settings.Default.Save();
            }

            _accountManager = new AccountManager();

            var savedAccount = _accountManager.CurrentAccount;

            if (savedAccount != null)
                SwitchAccount(savedAccount, false);

            AutoLoginCheckBox.IsChecked = Settings.IsAutologin();

            if (Settings.IsAutologin() && savedAccount != null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Log.Information("Engaging Autologin...");

                try
                {
                    #if DEBUG
                    HandleLogin(true);
                    Settings.Save();
                    return;
                    #else
                    if (!gateStatus)
                    {
                        MessageBox.Show(
                            "Square Enix seems to be running maintenance work right now. The game shouldn't be launched.");
                        Settings.SetAutologin(false);
                        _isLoggingIn = false;
                    }
                    else
                    {
                        HandleLogin(true);
                        Settings.Save();
                        return;
                    }
                    #endif
                }
                catch (Exception exc)
                {
                    new ErrorWindow(exc, "Additionally, please check your login information or try again.", "AutoLogin")
                        .ShowDialog();
                    Settings.SetAutologin(false);
                    _isLoggingIn = false;
                }

                Settings.Save();
            }

            if (Settings.GamePath == null)
            {
                var setup = new FirstTimeSetup();
                setup.ShowDialog();
            }

            Task.Run(() => SetupHeadlines());

            Settings.LanguageChanged += SetupHeadlines;

            Show();
            Activate();

            Log.Information("MainWindow initialized.");
        }

        private void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            Log.Information("AutoUpdaterOnCheckForUpdateEvent called.");
            if (args != null)
            {
                if (args.IsUpdateAvailable)
                {
                    try
                    {
                        Log.Information("Update available, trying to download.");
                        MessageBox.Show(
                            "An update for XIVLauncher is available. It will now be downloaded, the application will restart.",
                            "XIVLauncher Update", MessageBoxButton.OK, MessageBoxImage.Asterisk);

                        if (AutoUpdater.DownloadUpdate())
                        {
                            Environment.Exit(0);
                        }
                        else
                        {
                            Util.ShowError("Could not download update. Please try again later.", "Update failed");
                            Environment.Exit(0);
                        }
                    }
                    catch (Exception exc)
                    {
                        new ErrorWindow(exc, $"Update failed. Please report this error and try again later. \n\n{exc}",
                            "UpdateAvailableFail").ShowDialog();
                        Environment.Exit(0);
                    }
                }
                else
                {
                    Log.Information("No update: {0}", args.CurrentVersion);
                    InitializeWindow();
                }
            }
            else
            {
                Util.ShowError("Could not check for updates. Please try again later.", "Update failed");
                Log.Error("Update check failed.");
                Environment.Exit(0);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn)
                return;

            _isLoggingIn = true;
            HandleLogin(false);
        }


        #region Login

        private void HandleLogin(bool autoLogin)
        {
            var hasValidCache = _game.Cache.HasValidCache(LoginUsername.Text) && Settings.UniqueIdCacheEnabled;

            if (_accountManager.CurrentAccount != null && _accountManager.CurrentAccount.Password != LoginPassword.Password && _accountManager.CurrentAccount.SavePassword)
            {
                _accountManager.UpdatePassword(_accountManager.CurrentAccount, LoginPassword.Password);
            }

            if (_accountManager.CurrentAccount == null || _accountManager.CurrentAccount.Id != $"{LoginUsername.Text}-{OtpCheckBox.IsChecked == true}-{SteamCheckBox.IsChecked == true}")
            {
                var accountToSave = new XivAccount(LoginUsername.Text)
                {
                    Password = LoginPassword.Password,
                    SavePassword = true,
                    UseOtp = OtpCheckBox.IsChecked == true,
                    UseSteamServiceAccount = SteamCheckBox.IsChecked == true
                };

                _accountManager.AddAccount(accountToSave);

                Properties.Settings.Default.CurrentAccount = accountToSave.Id;
            }

            if (!autoLogin)
            {
                if (AutoLoginCheckBox.IsChecked == true)
                {
                    var result = MessageBox.Show(
                        "This option will log you in automatically with the credentials you entered.\nTo reset it again, launch this application while holding the Shift key.\n\nDo you really want to enable it?",
                        "Enabling Autologin", MessageBoxButton.YesNo);

                    if (result == MessageBoxResult.No) AutoLoginCheckBox.IsChecked = false;
                }
                else
                {
                    AutoLoginCheckBox.IsChecked = false;
                }

                Settings.SetAutologin(AutoLoginCheckBox.IsChecked == true);
            }

            Settings.Save();

            var otp = "";
            if (OtpCheckBox.IsChecked == true && !hasValidCache)
            {
                var otpDialog = new OtpInputDialog();
                otpDialog.ShowDialog();

                if (otpDialog.Result == null)
                {
                    _isLoggingIn = false;

                    if (autoLogin)
                        Environment.Exit(0);

                    return;
                }

                otp = otpDialog.Result;
            }

            StartGame(otp);
        }

        private async void StartGame(string otp)
        {
            Log.Information("StartGame() called");
            try
            {
                var gateStatus = false;
                try
                {
                    gateStatus = await Task.Run(() => _game.GetGateStatus());
                }
                catch
                {
                    // ignored
                }

                #if !DEBUG
                if (!gateStatus)
                {
                    Log.Information("GateStatus is false.");
                    MessageBox.Show(
                        "Square Enix seems to be running maintenance work right now or the login server is unreachable. The game shouldn't be launched.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    _isLoggingIn = false;

                    return;
                }
                #endif

                var loginResult = _game.Login(LoginUsername.Text, LoginPassword.Password, otp, SteamCheckBox.IsChecked == true, Settings.UniqueIdCacheEnabled);

                if (loginResult == null)
                {
                    Log.Information("LoginResult was null...");
                    _isLoggingIn = false;
                    return;
                }

                if (loginResult.State == XivGame.LoginState.NeedsPatch)
                {
                    /*
                    var patcher = new Game.Patch.PatchInstaller(_game, "ffxiv"); 
                    //var window = new IntegrityCheckProgressWindow();
                    var progress = new Progress<PatchDownloadProgress>();
                    progress.ProgressChanged += (sender, checkProgress) => Log.Verbose("PROGRESS");

                    Task.Run(async () => await patcher.DownloadPatchesAsync(loginResult.PendingPatches, loginResult.OauthLogin.SessionId, progress)).ContinueWith(task =>
                    {
                        //window.Dispatcher.Invoke(() => window.Close());
                        MessageBox.Show("Download OK");
                    });
                    */
                    return;
                }

                var gameProcess = XivGame.LaunchGame(loginResult.UniqueId, loginResult.OauthLogin.Region,
                    loginResult.OauthLogin.MaxExpansion, Settings.SteamIntegrationEnabled,
                    SteamCheckBox.IsChecked == true, Settings.AdditionalLaunchArgs);

                if (gameProcess == null)
                {
                    Log.Information("GameProcess was null...");
                    _isLoggingIn = false;
                    return;
                }

                var addonMgr = new AddonManager();

                try
                {
                    var addons = Settings.GetAddonList().Where(x => x.IsEnabled).ToList();
                    /*

                    addons.Add(new AddonEntry{
                            Addon = new CharacterBackupAddon()
                        });

                    if (Settings.CharacterSyncEnabled)
                        addons.Add(new AddonEntry{
                            Addon = new CharacterSyncAddon()
                        });
                        */

                    var backupDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "charDataBackup"));

                    if (backupDirectory.Exists)
                        backupDirectory.Delete(true);

                    await Task.Run(() => addonMgr.RunAddons(gameProcess, addons));
                }
                catch (Exception ex)
                {
                    new ErrorWindow(ex,
                        "This could be caused by your antivirus, please check its logs and add any needed exclusions.",
                        "Addons").ShowDialog();
                    _isLoggingIn = false;

                    addonMgr.StopAddons();
                }

                try
                {
                    if (Settings.IsInGameAddonEnabled())
                    {
                        var hooks = new DalamudLauncher(gameProcess);
                        hooks.Run();
                    }
                }
                catch (Exception ex)
                {
                    new ErrorWindow(ex,
                        "This could be caused by your antivirus, please check its logs and add any needed exclusions.",
                        "Hooks").ShowDialog();
                    _isLoggingIn = false;

                    addonMgr.StopAddons();
                }

                this.Close();
                
                var watchThread = new Thread(() =>
                {
                    while (!gameProcess.HasExited)
                    {
                        gameProcess.Refresh();
                        Thread.Sleep(1);
                    }

                    Log.Information("Game has exited.");
                    addonMgr.StopAddons();
                    Environment.Exit(0);
                });
                watchThread.Start();
            }
            catch (Exception ex)
            {
                new ErrorWindow(ex, "Please also check your login information or try again.", "Login").ShowDialog();
                _isLoggingIn = false;
            }
        }

        #endregion

        private void BannerCard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (_headlines != null) Process.Start(_headlines.Banner[_currentBannerIndex].Link.ToString());
        }

        private void SaveLoginCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            AutoLoginCheckBox.IsEnabled = true;
        }

        private void SaveLoginCheckBox_OnUnchecked(object sender, RoutedEventArgs e)
        {
            AutoLoginCheckBox.IsChecked = false;
            AutoLoginCheckBox.IsEnabled = false;
        }

        private void NewsListView_OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (_headlines == null)
                return;

            if (NewsListView.SelectedItem is News item)
            {
                if (item.Url != string.Empty)
                {
                    Process.Start(item.Url);
                }
                else
                {
                    string url;
                    switch (Settings.GetLanguage())
                    {
                        case ClientLanguage.Japanese:

                            url = "https://jp.finalfantasyxiv.com/lodestone/news/detail/";
                            break;

                        case ClientLanguage.English:

                            url = "https://eu.finalfantasyxiv.com/lodestone/news/detail/";
                            break;

                        case ClientLanguage.German:

                            url = "https://de.finalfantasyxiv.com/lodestone/news/detail/";
                            break;

                        case ClientLanguage.French:

                            url = "https://fr.finalfantasyxiv.com/lodestone/news/detail/";
                            break;

                        default:

                            url = "https://eu.finalfantasyxiv.com/lodestone/news/detail/";
                            break;
                    }

                    Process.Start(url + item.Id);
                }
            }
        }

        private void WorldStatusButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://is.xivup.com/");
        }

        private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
        {
            new SettingsWindow().ShowDialog();
        }

        private void QueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            _maintenanceQueueTimer = new Timer
            {
                Interval = 5000
            };

            _maintenanceQueueTimer.Elapsed += (o, args) =>
            {
                bool gateStatus;
                try
                {
                    gateStatus = _game.GetGateStatus();
                }
                catch
                {
                    // If getting our gate status fails, we shouldn't even bother
                    return;
                }

                if (gateStatus)
                {
                    Console.Beep(529, 130);
                    Thread.Sleep(200);
                    Console.Beep(529, 100);
                    Thread.Sleep(30);
                    Console.Beep(529, 100);
                    Thread.Sleep(300);
                    Console.Beep(420, 140);
                    Thread.Sleep(300);
                    Console.Beep(466, 100);
                    Thread.Sleep(300);
                    Console.Beep(529, 160);
                    Thread.Sleep(200);
                    Console.Beep(466, 100);
                    Thread.Sleep(30);
                    Console.Beep(529, 900);

                    Dispatcher.BeginInvoke(new Action(() => LoginButton_Click(null, null)));
                    _maintenanceQueueTimer.Stop();
                    return;
                }

                _maintenanceQueueTimer.Start();
            };

            DialogHost.OpenDialogCommand.Execute(null, MaintenanceQueueDialogHost);
            _maintenanceQueueTimer.Start();
        }

        private void QuitMaintenanceQueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            _maintenanceQueueTimer.Stop();
            DialogHost.CloseDialogCommand.Execute(null, MaintenanceQueueDialogHost);
        }

        private void Card_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return || _isLoggingIn)
                return;

            HandleLogin(false);
            _isLoggingIn = true;
        }

        private void MainWindow_OnClosed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void AccountSwitcherButton_OnClick(object sender, RoutedEventArgs e)
        {
            var switcher = new AccountSwitcher(_accountManager);

            switcher.WindowStartupLocation = WindowStartupLocation.Manual;
            var location = AccountSwitcherButton.PointToScreen(new Point(0,0));
            switcher.Left = location.X + 15;
            switcher.Top = location.Y + 15;

            switcher.OnAccountSwitchedEventHandler += OnAccountSwitchedEventHandler;

            switcher.Show();
        }

        private void OnAccountSwitchedEventHandler(object sender, XivAccount e)
        {
            SwitchAccount(e, true);
        }

        private void SwitchAccount(XivAccount account, bool saveAsCurrent)
        {
            LoginUsername.Text = account.UserName;
            OtpCheckBox.IsChecked = account.UseOtp;
            SteamCheckBox.IsChecked = account.UseSteamServiceAccount;
            AutoLoginCheckBox.IsChecked = Settings.IsAutologin();

            if (account.SavePassword)
                LoginPassword.Password = account.Password;

            if (saveAsCurrent)
            {
                Properties.Settings.Default.CurrentAccount = account.Id;
                Properties.Settings.Default.Save();
            }
        }
    }
}