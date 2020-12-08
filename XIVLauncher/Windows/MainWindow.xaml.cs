using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CheapLoc;
using MaterialDesignThemes.Wpf;
using Serilog;
using XIVLauncher.Accounts;
using XIVLauncher.Addon;
using XIVLauncher.Cache;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;
using XIVLauncher.Game.Patch;
using XIVLauncher.PatchInstaller;
using XIVLauncher.Settings;
using XIVLauncher.Windows.ViewModel;
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

        private readonly Launcher _launcher = new Launcher();
        private readonly Game.Patch.PatchInstaller _installer = new Game.Patch.PatchInstaller();

        private AccountManager _accountManager;

        private bool _isLoggingIn;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = new MainWindowViewModel();

            NewsListView.ItemsSource = new List<News>
            {
                new News
                {
                    Title = Loc.Localize("NewsLoading", "Loading..."),
                    Tag = "DlError"
                }
            };

#if !XL_NOAUTOUPDATE
            Title += " v" + Util.GetAssemblyVersion();
#else
            Title += " " + Util.GetGitHash();
#endif

#if XL_NOAUTOUPDATE
            Title += " - UNSUPPORTED VERSION - NO UPDATES - COULD DO BAD THINGS";
#endif

            if (Util.IsWine)
                Title += " - Wine on Linux";
        }

        private void SetupHeadlines()
        {
            try
            {
                _bannerChangeTimer?.Stop();

                _headlines = Headlines.Get(_launcher, App.Settings.Language.GetValueOrDefault(ClientLanguage.English));

                _bannerBitmaps = new BitmapImage[_headlines.Banner.Length];
                for (var i = 0; i < _headlines.Banner.Length; i++)
                {
                    var imageBytes = _launcher.DownloadAsLauncher(_headlines.Banner[i].LsbBanner.ToString(), App.Settings.Language.GetValueOrDefault(ClientLanguage.English));

                    using var stream = new MemoryStream(imageBytes);

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    _bannerBitmaps[i] = bitmapImage;
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
                    NewsListView.ItemsSource = new List<News> {new News {Title = Loc.Localize("NewsDlFailed", "Could not download news data."), Tag = "DlError"}};
                }));
            }
        }

        public void Initialize()
        {
            // Upgrade the stored settings if needed
            if (Properties.Settings.Default.UpgradeRequired)
            {
                Log.Information("Settings upgrade required...");
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpgradeRequired = false;
                Properties.Settings.Default.Save();
            }

            // Clean up invalid addons
            if (App.Settings.AddonList != null)
                App.Settings.AddonList = App.Settings.AddonList.Where(x => !string.IsNullOrEmpty(x.Addon.Path)).ToList();

            App.Settings.EncryptArguments ??= true;
            App.Settings.AskBeforePatchInstall ??= true;

            var gateStatus = false;
            try
            {
                gateStatus = _launcher.GetGateStatus();
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

            _accountManager = new AccountManager(App.Settings);

            var savedAccount = _accountManager.CurrentAccount;

            if (savedAccount != null)
                SwitchAccount(savedAccount, false);

            AutoLoginCheckBox.IsChecked = App.Settings.AutologinEnabled;

            if (App.Settings.AutologinEnabled && savedAccount != null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Log.Information("Engaging Autologin...");

                try
                {
                    HandleLogin(true);
                    return;
                }
                catch (Exception ex)
                {
                    new ErrorWindow(ex, Loc.Localize("CheckLoginInfo", "Additionally, please check your login information or try again."), "AutoLogin")
                        .ShowDialog();
                    App.Settings.AutologinEnabled = false;
                    _isLoggingIn = false;
                }
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || bool.Parse(Environment.GetEnvironmentVariable("XL_NOAUTOLOGIN") ?? "false"))
            {
                App.Settings.AutologinEnabled = false;
                AutoLoginCheckBox.IsChecked = false;
            }

            if (App.Settings.GamePath?.Exists != true)
            {
                var setup = new FirstTimeSetup();
                setup.ShowDialog();
                
                SettingsControl.ReloadSettings();
            }

            Task.Run(SetupHeadlines);

            ProblemCheck.RunCheck();

            Log.Information("MainWindow initialized.");

            Show();
            Activate();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LoginUsername.Text))
            {
                MessageBox.Show(
                    Loc.Localize("EmptyUsernameError", "Please enter an username."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(LoginPassword.Password))
            {
                MessageBox.Show(
                    Loc.Localize("EmptyPasswordError", "Please enter a password."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (_isLoggingIn)
                return;

            _isLoggingIn = true;
            HandleLogin(false);
        }

        private void HandleBootCheck(Action whenFinishAction)
        {
            try
            {
                App.Settings.PatchPath ??= new DirectoryInfo(Path.Combine(Paths.RoamingPath, "patches"));

                var bootPatches = _launcher.CheckBootVersion(App.Settings.GamePath);
                if (bootPatches != null)
                {
                    var mutex = new Mutex(false, "XivLauncherIsPatching");
                    if (mutex.WaitOne(0, false))
                    {
                        if (Util.CheckIsGameOpen())
                        {
                            MessageBox.Show(
                                Loc.Localize("GameIsOpenError",
                                    "The game and/or the official launcher are open. XIVLauncher cannot patch the game if this is the case.\nPlease close the official launcher and try again."),
                                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                            Environment.Exit(0);
                            return;
                        }

                        var patcher = new PatchManager(Repository.Boot, bootPatches, App.Settings.GamePath,
                            App.Settings.PatchPath, _installer);

                        var progressDialog = new PatchDownloadDialog(patcher);
                        progressDialog.Show();
                        this.Hide();

                        patcher.OnFinish += (sender, args) =>
                        {
                            if (args)
                            {
                                progressDialog.Dispatcher.Invoke(() => progressDialog.Close());
                                whenFinishAction?.Invoke();
                            }
                            else
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    this.Show();
                                    _isLoggingIn = false;
                                });
                            }
                            
                            mutex.Close();
                            mutex = null;
                        };

                        patcher.Start();
                    }
                    else
                    {
                        MessageBox.Show(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."), "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                        Environment.Exit(0);
                    }
                }
                else
                {
                    whenFinishAction?.Invoke();
                }
            }
            catch (Exception ex)
            {
                new ErrorWindow(ex, "Could not patch boot.", nameof(HandleBootCheck)).ShowDialog();
                Environment.Exit(0);
            }
        }

        private void HandleLogin(bool autoLogin)
        {
            var hasValidCache = _launcher.Cache.HasValidCache(LoginUsername.Text) && App.Settings.UniqueIdCacheEnabled;

            Log.Information("CurrentAccount: {0}", _accountManager.CurrentAccount == null ? "null" : _accountManager.CurrentAccount.ToString());

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
                App.Settings.AutologinEnabled = AutoLoginCheckBox.IsChecked == true;
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
                    {
                        CleanUp();
                        Environment.Exit(0);
                    }

                    return;
                }

                otp = otpDialog.Result;
            }

            HandleBootCheck(() => this.Dispatcher.Invoke(() => StartLogin(otp)));
        }

        private void InstallGamePatch(Launcher.LoginResult loginResult, bool gateStatus)
        {
            var mutex = new Mutex(false, "XivLauncherIsPatching");
            if (mutex.WaitOne(0, false))
            {
                if (Util.CheckIsGameOpen())
                {
                    MessageBox.Show(
                        Loc.Localize("GameIsOpenError", "The game and/or the official launcher are open. XIVLauncher cannot patch the game if this is the case.\nPlease close the official launcher and try again."),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    this.Dispatcher.Invoke(() =>
                    {
                        this.Show();
                        _isLoggingIn = false;
                    });
                }
                else
                {
                    Debug.Assert(loginResult.State == Launcher.LoginState.NeedsPatchGame,
                        "loginResult.State == Launcher.LoginState.NeedsPatchGame ASSERTION FAILED");

                    var patcher = new PatchManager(Repository.Ffxiv, loginResult.PendingPatches, App.Settings.GamePath, App.Settings.PatchPath, _installer);

                    var progressDialog = new PatchDownloadDialog(patcher);
                    progressDialog.Show();
                    this.Hide();

                    patcher.OnFinish += async (sender, args) =>
                    {
                        progressDialog.Dispatcher.Invoke(() => progressDialog.Close());

                        if (args)
                        {
                            await this.Dispatcher.Invoke(() => StartGameAndAddon(loginResult, gateStatus));
                            _installer.Stop();
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                this.Show();
                                _isLoggingIn = false;
                            });
                        }

                        mutex.Close();
                        mutex = null;
                    };

                    patcher.Start();
                }
            }
            else
            {
                MessageBox.Show(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."), "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
        }

        private async void StartLogin(string otp)
        {
            Log.Information("StartGame() called");

            var gateStatus = false;
            try
            {
                gateStatus = await Task.Run(() => _launcher.GetGateStatus());
            }
            catch
            {
                // ignored
            }

            try
            {
                var loginResult = _launcher.Login(LoginUsername.Text, LoginPassword.Password, otp, SteamCheckBox.IsChecked == true, App.Settings.UniqueIdCacheEnabled, App.Settings.GamePath);

                if (loginResult == null)
                {
                    Log.Information("LoginResult was null...");
                    _isLoggingIn = false;

                    // If this is an autologin, we don't want to stick around after a failed login
                    if (AutoLoginCheckBox.IsChecked == true)
                    {
                        Close();
                        CleanUp();
                        Environment.Exit(0);
                    }

                    return;
                }

                if (loginResult.State != Launcher.LoginState.Ok)
                {
                    if (App.Settings.AskBeforePatchInstall.HasValue && App.Settings.AskBeforePatchInstall.Value)
                    {
                        var selfPatchAsk = MessageBox.Show(
                            Loc.Localize("PatchInstallDisclaimer", "A new patch has been found that needs to be installed before you can play.\nDo you wish for XIVLauncher to install it?\nThis is an experimental feature, but has been tested considerably and should work fine."),
                            "Out of date", MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (selfPatchAsk == MessageBoxResult.Yes)
                        {
                            InstallGamePatch(loginResult, gateStatus);
                        }
                        else
                        {
                            _isLoggingIn = false;
                            return;
                        }
                    }
                    else
                    {
                        InstallGamePatch(loginResult, gateStatus);
                    }

                    return;
                }

                await StartGameAndAddon(loginResult, gateStatus);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "StartGame failed...");

                if (!gateStatus)
                {
                    Log.Information("GateStatus is false.");
                    MessageBox.Show(
                        Loc.Localize("MaintenanceNotice", "Maintenance seems to be in progress. The game shouldn't be launched."),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    
                    _isLoggingIn = false;

                    Show();
                    Activate();
                    return;
                }

                new ErrorWindow(ex, "Please also check your login information or try again.", "Login").ShowDialog();
            }
        }

        private async Task StartGameAndAddon(Launcher.LoginResult loginResult, bool gateStatus)
        {
            if (!gateStatus)
            {
                Log.Information("GateStatus is false.");
                MessageBox.Show(
                    Loc.Localize("MaintenanceNotice",
                        "Maintenance seems to be in progress. The game shouldn't be launched."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                _isLoggingIn = false;

                Show();
                Activate();

                return;
            }

            // We won't do any sanity checks here anymore, since that should be handled in StartLogin

            var gameProcess = Launcher.LaunchGame(loginResult.UniqueId, loginResult.OauthLogin.Region,
                    loginResult.OauthLogin.MaxExpansion, App.Settings.SteamIntegrationEnabled,
                    SteamCheckBox.IsChecked == true, App.Settings.AdditionalLaunchArgs, App.Settings.GamePath, App.Settings.IsDx11, App.Settings.Language.GetValueOrDefault(ClientLanguage.English), App.Settings.EncryptArguments.GetValueOrDefault(false));

            if (gameProcess == null)
            {
                Log.Information("GameProcess was null...");
                _isLoggingIn = false;
                return;
            }

            CleanUp();

            this.Hide();

            var addonMgr = new AddonManager();

            try
            {
                var addons = App.Settings.AddonList.Where(x => x.IsEnabled).Select(x => x.Addon).Cast<IAddon>().ToList();

                if (App.Settings.InGameAddonEnabled && App.Settings.IsDx11)
                {
                    addons.Add(new DalamudLauncher());
                }

                await Task.Run(() => addonMgr.RunAddons(gameProcess, App.Settings, addons));
            }
            catch (Exception ex)
            {
                new ErrorWindow(ex,
                    "This could be caused by your antivirus, please check its logs and add any needed exclusions.",
                    "Addons").ShowDialog();
                _isLoggingIn = false;

                addonMgr.StopAddons();
            }

            var watchThread = new Thread(() =>
            {
                while (!gameProcess.HasExited)
                {
                    gameProcess.Refresh();
                    Thread.Sleep(1);
                }

                Log.Information("Game has exited.");
                addonMgr.StopAddons();
                
                CleanUp();

                Environment.Exit(0);
            });
            watchThread.Start();

            Log.Debug("Started WatchThread");
        }

        private void CleanUp()
        {
            _installer.Stop();
        }

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

            if (!(NewsListView.SelectedItem is News item)) 
                return;

            if (!string.IsNullOrEmpty(item.Url))
            {
                Process.Start(item.Url);
            }
            else
            {
                string url;
                switch (App.Settings.Language)
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

        private void WorldStatusButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://is.xivup.com/");
        }

        private void QueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_maintenanceQueueTimer == null)
                SetupMaintenanceQueueTimer();

            DialogHost.OpenDialogCommand.Execute(null, MaintenanceQueueDialogHost);
            _maintenanceQueueTimer.Start();
        }

        private void SetupMaintenanceQueueTimer()
        {
            _maintenanceQueueTimer = new Timer
            {
                Interval = 15000
            };

            _maintenanceQueueTimer.Elapsed += (o, args) =>
            {
                var bootPatches = _launcher.CheckBootVersion(App.Settings.GamePath);

                var gateStatus = false;
                try
                {
                    gateStatus = _launcher.GetGateStatus();
                }
                catch
                {
                    // ignored
                }

                if (gateStatus || bootPatches != null)
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

                    if (bootPatches != null)
                        MessageBox.Show(Loc.Localize("MaintenanceQueueBootPatch",
                            "A patch for the FFXIV launcher was detected.\nThis usually means that there is a patch for the game as well.\n\nYou will now be logged in."));

                    Dispatcher.BeginInvoke(new Action(() => {
                        LoginButton_Click(null, null);
                        QuitMaintenanceQueueButton_OnClick(null, null);
                    }));
                }
            };
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
            CleanUp();
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
            AutoLoginCheckBox.IsChecked = App.Settings.AutologinEnabled;

            if (account.SavePassword)
                LoginPassword.Password = account.Password;

            if (saveAsCurrent)
            {
                _accountManager.CurrentAccount = account;
            }
        }

        private void SettingsControl_OnSettingsDismissed(object sender, EventArgs e)
        {
            Task.Run(SetupHeadlines);
        }
    }
}