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
using MaterialDesignThemes.Wpf;
using Serilog;
using XIVLauncher.Accounts;
using XIVLauncher.Addon;
using XIVLauncher.Addon.Implementations;
using XIVLauncher.Cache;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;
using XIVLauncher.Game.Patch;
using XIVLauncher.Settings;
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

        private readonly XivGame _game;

        private AccountManager _accountManager;

        private bool _isLoggingIn;

        private ILauncherSettingsV3 _setting;

        public MainWindow(ILauncherSettingsV3 setting, string accountName)
        {
            _setting = setting;
            _game = new XivGame(setting);

            InitializeComponent();

#if !XL_NOAUTOUPDATE
            Title += " v" + Util.GetAssemblyVersion();
#else
            Title += " " + Util.GetGitHash();
#endif

            if (!string.IsNullOrEmpty(accountName))
            {
                Properties.Settings.Default.CurrentAccount = accountName;
            }

#if XL_NOAUTOUPDATE
            Title += " - UNSUPPORTED VERSION - NO UPDATES - COULD DO BAD THINGS";
#endif

            InitializeWindow();
        }

        private void SetupHeadlines()
        {
            try
            {
                _bannerChangeTimer?.Stop();

                _headlines = Headlines.Get(_game, _setting.Language);

                _bannerBitmaps = new BitmapImage[_headlines.Banner.Length];
                for (var i = 0; i < _headlines.Banner.Length; i++)
                {
                    var imageBytes = _game.DownloadAsLauncher(_headlines.Banner[i].LsbBanner.ToString(), _setting.Language);

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

            LauncherSettings.TryMigrate(_setting);

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

                Properties.Settings.Default.Save();
            }

            _accountManager = new AccountManager(_setting);

            var savedAccount = _accountManager.CurrentAccount;

            if (savedAccount != null)
                SwitchAccount(savedAccount, false);

            AutoLoginCheckBox.IsChecked = _setting.AutologinEnabled;

            if (_setting.AutologinEnabled && savedAccount != null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Log.Information("Engaging Autologin...");

                try
                {
#if DEBUG
                    HandleLogin(true);
                    return;
#else
                    if (!gateStatus)
                    {
                        var startLauncher = MessageBox.Show(
                            "Square Enix seems to be running maintenance work right now. The game shouldn't be launched. Do you want to start the official launcher to check for patches?", "XIVLauncher", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

                        if (startLauncher)
                            Util.StartOfficialLauncher(_setting.GamePath, SteamCheckBox.IsChecked == true);

                        _setting.AutologinEnabled = false;
                        _isLoggingIn = false;
                    }
                    else
                    {
                        HandleLogin(true);
                        return;
                    }
#endif
                }
                catch (Exception ex)
                {
                    new ErrorWindow(ex, "Additionally, please check your login information or try again.", "AutoLogin", _setting)
                        .ShowDialog();
                    _setting.AutologinEnabled = false;
                    _isLoggingIn = false;
                }
            }

            if (_setting.GamePath?.Exists != true)
            {
                var setup = new FirstTimeSetup(_setting);
                setup.ShowDialog();
            }

            Task.Run(() => SetupHeadlines());

            AdminCheck.RunCheck();

            Show();
            Activate();

            Log.Information("MainWindow initialized.");
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
            var hasValidCache = _game.Cache.HasValidCache(LoginUsername.Text) && _setting.UniqueIdCacheEnabled;

            if (_accountManager.CurrentAccount != null && _accountManager.CurrentAccount.UserName.Equals(LoginUsername.Text) && _accountManager.CurrentAccount.Password != LoginPassword.Password && _accountManager.CurrentAccount.SavePassword)
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

                _accountManager.CurrentAccount = accountToSave;
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

                _setting.AutologinEnabled = AutoLoginCheckBox.IsChecked == true;
            }

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
                    var startLauncher = MessageBox.Show(
                                             "Square Enix seems to be running maintenance work right now. The game shouldn't be launched. Do you want to start the official launcher to check for patches?", "XIVLauncher", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

                    if (startLauncher)
                        Util.StartOfficialLauncher(_setting.GamePath, SteamCheckBox.IsChecked == true);

                    _isLoggingIn = false;

                    return;
                }
#endif

                var loginResult = _game.Login(LoginUsername.Text, LoginPassword.Password, otp, SteamCheckBox.IsChecked == true, _setting.UniqueIdCacheEnabled, _setting.GamePath);

                if (loginResult == null)
                {
                    Log.Information("LoginResult was null...");
                    _isLoggingIn = false;

                    // If this is an autologin, we don't want to stick around after a failed login
                    if (AutoLoginCheckBox.IsChecked == true)
                    {
                        Close();
                        Environment.Exit(0);
                    }

                    return;
                }

                if (loginResult.State != XivGame.LoginState.Ok)
                {
                    var msgBoxResult = MessageBox.Show(
                        "Your game is out of date. Please start the official launcher and update it before trying to log in. Do you want to start the official launcher?",
                        "Out of date", MessageBoxButton.YesNo, MessageBoxImage.Error);

                    if (msgBoxResult == MessageBoxResult.Yes)
                        Util.StartOfficialLauncher(_setting.GamePath, SteamCheckBox.IsChecked == true);

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
                    loginResult.OauthLogin.MaxExpansion, _setting.SteamIntegrationEnabled,
                    SteamCheckBox.IsChecked == true, _setting.AdditionalLaunchArgs, _setting.GamePath, _setting.IsDx11, _setting.Language);

                if (gameProcess == null)
                {
                    Log.Information("GameProcess was null...");
                    _isLoggingIn = false;
                    return;
                }

                var addonMgr = new AddonManager();

                try
                {
                    var addons = _setting.AddonList.Where(x => x.IsEnabled).ToList();

                    addons.Add(new AddonEntry{
                            Addon = new CharacterBackupAddon()
                        });

                    if (_setting.CharacterSyncEnabled)
                        addons.Add(new AddonEntry{
                            Addon = new CharacterSyncAddon()
                        });

                    var backupDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "charDataBackup"));

                    if (backupDirectory.Exists)
                        backupDirectory.Delete(true);

                    await Task.Run(() => addonMgr.RunAddons(gameProcess, _setting, addons));
                }
                catch (Exception ex)
                {
                    new ErrorWindow(ex,
                        "This could be caused by your antivirus, please check its logs and add any needed exclusions.",
                        "Addons", _setting).ShowDialog();
                    _isLoggingIn = false;

                    addonMgr.StopAddons();
                }

                try
                {
                    if (_setting.InGameAddonEnabled && _setting.IsDx11)
                    {
                        var hooks = new DalamudLauncher(gameProcess);
                        hooks.Run(_setting.GamePath, _setting.Language);
                    }
                }
                catch (Exception ex)
                {
                    new ErrorWindow(ex,
                        "This could be caused by your antivirus, please check its logs and add any needed exclusions.",
                        "Hooks", _setting).ShowDialog();
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
                new ErrorWindow(ex, "Please also check your login information or try again.", "Login", _setting).ShowDialog();
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
                    switch (_setting.Language)
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
            var oldLanguage = _setting.Language;
            new SettingsWindow(_setting).ShowDialog();

            // Language changed? Let's redownload headlines and banners
            if (oldLanguage != _setting.Language)
                SetupHeadlines();
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
            AutoLoginCheckBox.IsChecked = _setting.AutologinEnabled;

            if (account.SavePassword)
                LoginPassword.Password = account.Password;

            if (saveAsCurrent)
            {
                _accountManager.CurrentAccount = account;
            }
        }
    }
}