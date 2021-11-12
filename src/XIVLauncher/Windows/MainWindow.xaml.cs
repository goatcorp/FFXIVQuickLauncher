using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
using XIVLauncher.Game.Patch.Acquisition;
using XIVLauncher.PatchInstaller;
using XIVLauncher.PatchInstaller.PatcherIpcMessages;
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

        private MainWindowViewModel Model => this.DataContext as MainWindowViewModel;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = new MainWindowViewModel();
            Closed += Model.OnWindowClosed;
            Closing += Model.OnWindowClosing;

            Model.Activate += () => this.Dispatcher.Invoke(() =>
            {
                this.Show();
                this.Activate();
                this.Focus();
            });

            Model.Hide += () => this.Dispatcher.Invoke(() =>
            {
                this.Hide();
            });

            Model.ReloadHeadlines += () => Task.Run(SetupHeadlines);

            Model.PatchDownloadDialogFactory = patcher =>
            {
                var dialog = new PatchDownloadDialog(patcher);

                if (this.IsVisible)
                {
                    dialog.Owner = this;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                return dialog;
            };

            Model.OtpInputDialogFactory = () =>
            {
                var dialog = new OtpInputDialog();

                if (this.IsVisible)
                {
                    dialog.Owner = this;
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                return dialog;
            };

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

#if !XL_NOAUTOUPDATE
            if (EnvironmentSettings.IsDisableUpdates)
#endif
            {
                Title += " - UNSUPPORTED VERSION - NO UPDATES - COULD DO BAD THINGS";
            }

#if DEBUG
            Title += " - Debugging";
#endif

            if (EnvironmentSettings.IsWine)
                Title += " - Wine on Linux";
        }

        private async Task SetupHeadlines()
        {
            try
            {
                _bannerChangeTimer?.Stop();

                _headlines = await Headlines.Get(_launcher, App.Settings.Language.GetValueOrDefault(ClientLanguage.English));

                _bannerBitmaps = new BitmapImage[_headlines.Banner.Length];
                for (var i = 0; i < _headlines.Banner.Length; i++)
                {
                    var imageBytes = await _launcher.DownloadAsLauncher(_headlines.Banner[i].LsbBanner.ToString(), App.Settings.Language.GetValueOrDefault(ClientLanguage.English));

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
#if DEBUG
            var fakeStartMenuItem = new MenuItem
            {
                Header = "Fake start"
            };
            fakeStartMenuItem.Click += FakeStart_OnClick;

            LoginContextMenu.Items.Add(fakeStartMenuItem);
#endif

            // Set the default patch acquisition method
            App.Settings.PatchAcquisitionMethod ??=
                EnvironmentSettings.IsWine ? AcquisitionMethod.NetDownloader : AcquisitionMethod.Aria;

            // Clean up invalid addons
            if (App.Settings.AddonList != null)
                App.Settings.AddonList = App.Settings.AddonList.Where(x => !string.IsNullOrEmpty(x.Addon.Path)).ToList();

            App.Settings.EncryptArguments ??= true;
            App.Settings.AskBeforePatchInstall ??= true;

            var gateStatus = false;
            try
            {
                gateStatus = Task.Run(() => _launcher.GetGateStatus()).Result;
            }
            catch
            {
                // ignored
            }

            if (!gateStatus) WorldStatusPackIcon.Foreground = new SolidColorBrush(Color.FromRgb(242, 24, 24));

            var version = Util.GetAssemblyVersion();
            if (App.Settings.LastVersion != version)
            {
                new ChangelogWindow().ShowDialog();

                App.Settings.LastVersion = version;
            }

            _accountManager = new AccountManager(App.Settings);

            var savedAccount = _accountManager.CurrentAccount;

            if (savedAccount != null)
                SwitchAccount(savedAccount, false);

            Model.IsAutoLogin = App.Settings.AutologinEnabled;

            if (App.Settings.UniqueIdCacheEnabled && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                UniqueIdCache.Instance.Reset();
                Console.Beep(523, 150); // Feedback without popup
            }

            if (App.GlobalIsDisableAutologin)
            {
                Log.Information("Autologin was disabled globally, saving into settings...");
                App.Settings.AutologinEnabled = false;
            }

            if (App.Settings.AutologinEnabled && savedAccount != null && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                Log.Information("Engaging Autologin...");

                try
                {
                    Model.IsLoggingIn = true;
                    Dispatcher.InvokeAsync(() => Model.Login(savedAccount.UserName, savedAccount.Password,
                        savedAccount.UseOtp,
                        savedAccount.UseSteamServiceAccount, true, true));

                    return;
                }
                catch (Exception ex)
                {
                    new ErrorWindow(ex, Loc.Localize("CheckLoginInfo", "Additionally, please check your login information or try again."), "AutoLogin")
                        .ShowDialog();
                    App.Settings.AutologinEnabled = false;
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

                // If the user didn't reach the end of the setup, we should quit
                if (!setup.WasCompleted)
                {
                    Environment.Exit(0);
                    return;
                }

                SettingsControl.ReloadSettings();
            }

            Task.Run(SetupHeadlines);

            Log.Information("MainWindow initialized.");

            Show();
            Activate();
        }

        private void BannerCard_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (_headlines != null) Process.Start(_headlines.Banner[_currentBannerIndex].Link.ToString());
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

                    case ClientLanguage.English when Util.IsRegionNorthAmerica():
                        url = "https://na.finalfantasyxiv.com/lodestone/news/detail/";
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

            Model.LoadingDialogCancelButtonVisibility = Visibility.Visible;
            Model.LoadingDialogMessage = Model.WaitingForMaintenanceLoc;
            Model.IsLoadingDialogOpen = true;

            _maintenanceQueueTimer.Start();

            // Manually fire the first event, avoid waiting the first timer interval
            Task.Run(() =>
            {
                OnMaintenanceQueueTimerEvent(null, null);
            });
        }

        private void SetupMaintenanceQueueTimer()
        {
            // This is a good indicator that we should clear the UID cache
            UniqueIdCache.Instance.Reset();

            _maintenanceQueueTimer = new Timer
            {
                Interval = 15000
            };

            _maintenanceQueueTimer.Elapsed += OnMaintenanceQueueTimerEvent;
        }

        private void OnMaintenanceQueueTimerEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            var bootPatches = _launcher.CheckBootVersion(App.Settings.GamePath);

            var gateStatus = false;
            try
            {
                gateStatus = Task.Run(() => _launcher.GetGateStatus()).Result;
            }
            catch
            {
                // ignored
            }

            if (gateStatus || bootPatches != null)
            {
                if (bootPatches != null)
                    CustomMessageBox.Show(Loc.Localize("MaintenanceQueueBootPatch",
                        "A patch for the FFXIV launcher was detected.\nThis usually means that there is a patch for the game as well.\n\nYou will now be logged in."), "XIVLauncher");

                Dispatcher.InvokeAsync(async () => {
                    QuitMaintenanceQueueButton_OnClick(null, null);

                    if (Model.IsLoggingIn)
                        return;

                    Model.IsLoggingIn = true;
                    await Model.Login(Model.Username, LoginPassword.Password, Model.IsOtp, Model.IsSteam, false, true);
                    Model.IsLoggingIn = false;
                });

                Console.Beep(523, 150);
                Thread.Sleep(25);
                Console.Beep(523, 150);
                Thread.Sleep(25);
                Console.Beep(523, 150);
                Thread.Sleep(25);
                Console.Beep(523, 300);
                Thread.Sleep(150);
                Console.Beep(415, 300);
                Thread.Sleep(150);
                Console.Beep(466, 300);
                Thread.Sleep(150);
                Console.Beep(523, 300);
                Thread.Sleep(25);
                Console.Beep(466, 150);
                Thread.Sleep(25);
                Console.Beep(523, 900);
            }
        }

        private void QuitMaintenanceQueueButton_OnClick(object sender, RoutedEventArgs e)
        {
            _maintenanceQueueTimer.Stop();
            Model.IsLoadingDialogOpen = false;
        }

        private async void Card_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return)
                return;

            if (Model.IsLoggingIn)
                return;

            Model.IsLoggingIn = true;
            await Model.Login(Model.Username, LoginPassword.Password, Model.IsOtp, Model.IsSteam, false, true);
            Model.IsLoggingIn = false;
        }

        private void AccountSwitcherButton_OnClick(object sender, RoutedEventArgs e)
        {
            var switcher = new AccountSwitcher(_accountManager);

            switcher.WindowStartupLocation = WindowStartupLocation.Manual;
            var location = PointToScreen(Mouse.GetPosition(this));
            switcher.Left = location.X - 15;
            switcher.Top = location.Y - 15;

            switcher.OnAccountSwitchedEventHandler += OnAccountSwitchedEventHandler;

            switcher.Show();
        }

        private void OnAccountSwitchedEventHandler(object sender, XivAccount e)
        {
            SwitchAccount(e, true);
        }

        private void SwitchAccount(XivAccount account, bool saveAsCurrent)
        {
            Model.Username = account.UserName;
            Model.IsOtp = account.UseOtp;
            Model.IsSteam = account.UseSteamServiceAccount;
            Model.IsAutoLogin = App.Settings.AutologinEnabled;

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

        private void FakeStart_OnClick(object sender, RoutedEventArgs e)
        {
            Model.StartGameAndAddon(new Launcher.LoginResult
            {
                OauthLogin = new Launcher.OauthLoginResult
                {
                    MaxExpansion = 3,
                    Playable = true,
                    Region = 0,
                    SessionId = "0",
                    TermsAccepted = true
                },
                State = Launcher.LoginState.Ok,
                UniqueId = "0"
            }, true, false);
        }
    }
}