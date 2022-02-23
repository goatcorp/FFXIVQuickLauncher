using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CheapLoc;
using Serilog;
using XIVLauncher.Accounts;
using XIVLauncher.Addon;
using XIVLauncher.Common;
using XIVLauncher.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Game;
using XIVLauncher.PlatformAbstractions;
using XIVLauncher.Support;
using XIVLauncher.Xaml;

namespace XIVLauncher.Windows.ViewModel
{
    class MainWindowViewModel : INotifyPropertyChanged
    {
        public bool IsLoggingIn;

        private readonly Launcher _launcher = new(CommonRunner.Instance, CommonSteam.Instance, CommonUniqueIdCache.Instance, CommonSettings.Instance);
        private readonly Common.Game.Patch.PatchInstaller _installer = new(CommonSettings.Instance);

        public AccountManager AccountManager { get; private set; } = new(App.Settings);

        public Action Activate;
        public Action Hide;
        public Action ReloadHeadlines;

        public Func<PatchManager, PatchDownloadDialog> PatchDownloadDialogFactory { get; set; }
        public Func<PatchVerifier, GameRepairProgressWindow> GameRepairProgressWindowFactory { get; set; }
        public Func<OtpInputDialog> OtpInputDialogFactory { get; set; }

        public string Password { get; set; }

        public MainWindowViewModel()
        {
            SetupLoc();

            StartLoginCommand = new AsyncCommand(GetLoginFunc(true, false, false), () => !IsLoggingIn);
            LoginNoStartCommand = new AsyncCommand(GetLoginFunc(false, false, false), () => !IsLoggingIn);
            LoginNoDalamudCommand = new AsyncCommand(GetLoginFunc(true, false, true), () => !IsLoggingIn);
            LoginRepairCommand = new AsyncCommand(GetLoginFunc(false, true, false), () => !IsLoggingIn);
        }

        private Func<object, Task> GetLoginFunc(bool startGame, bool isRepair, bool forceNoDalamud)
        {
            return async p =>
            {
                if (IsAutoLogin && App.Settings.HasShownAutoLaunchDisclaimer.GetValueOrDefault(false) == false)
                {
                    CustomMessageBox.Show(Loc.Localize("AutoLoginIntro", "You are enabling Auto-Login.\nThis means that XIVLauncher will always log you in with the current account and you will not see this window.\n\nTo change settings and accounts, you have to hold the shift button on your keyboard while clicking the XIVLauncher icon."), "XIVLauncher");
                    App.Settings.HasShownAutoLaunchDisclaimer = true;
                }

                if (Util.CheckIsGameOpen() && isRepair)
                {
                    CustomMessageBox.Show(
                        Loc.Localize("GameIsOpenRepairError", "The game and/or the official launcher are open. XIVLauncher cannot repair the game if this is the case.\nPlease close them and try again."),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    Reactivate();
                    return;
                }

                if (isRepair)
                {
                    if (MessageBox.Show(Loc.Localize("GameRepairDisclaimer", "XIVLauncher will now try to find corrupted game files and repair them.\nIf you use any TexTools mods, this will replace all of them and restore the game to its initial state.\n\nDo you want to continue?"),
                            "XIVLauncher", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    {
                        Reactivate();
                        return;
                    }
                }

                LoadingDialogCancelButtonVisibility = Visibility.Collapsed;
                //IsLoadingDialogOpen = true;
                //LoadingDialogMessage = Loc.Localize("LoadingDialogIsLoggingIn", "Transmission in progress...");

                IsEnabled = false;
                LoginCardTransitionerIndex = 0;

                IsLoggingIn = true;

                await Login(Username, Password, IsOtp, IsSteam, false, startGame, isRepair, forceNoDalamud);

                //IsLoadingDialogOpen = false;
                LoginCardTransitionerIndex = 1;
                IsEnabled = true;
                IsLoggingIn = false;
            };
        }

        public async Task Login(string username, string password, bool isOtp, bool isSteam, bool doingAutoLogin, bool startGame, bool isRepair, bool forceNoDalamud)
        {
            ProblemCheck.RunCheck();

            /* ============= MARCH 2022 STEAM UPDATE ============= */
            var bootver = SeVersion.Parse(Repository.Boot.GetVer(App.Settings.GamePath));
            var ver600 = SeVersion.Parse("2021.11.16.0000.0001");
            if (bootver > ver600)
            {
                CustomMessageBox.Show(Loc.Localize("KillswitchText", "XIVLauncher cannot start the game at this time, as Square Enix has made changes to the login process." +
                                                                     "\nWe need to adjust to these changes and verify that our adjustments are safe before we can re-enable the launcher. Please try again later." +
                                                                     "\n\nWe apologize for these circumstances.\n\nYou can use the \"Official Launcher\" button below to start the official launcher." +
                                                                     "\n") + Loc.Localize("SteamLinkingText", "You may be prompted to link your Steam account to your Square Enix account.")
                    , "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.None, showHelpLinks: false, showDiscordLink: true, showOfficialLauncher: true);

                return;
            }
            /* =================================================== */

            var bootRes = await HandleBootCheck();

            if (!bootRes)
            {
                Reactivate();
                return;
            }

            if (string.IsNullOrEmpty(username))
            {
                CustomMessageBox.Show(
                    Loc.Localize("EmptyUsernameError", "Please enter an username."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);

                Reactivate();
                return;
            }

            if (username.Contains("@"))
            {
                CustomMessageBox.Show(
                    Loc.Localize("EmailUsernameError", "Please enter your SE account name, not your email address."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);

                Reactivate();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                CustomMessageBox.Show(
                    Loc.Localize("EmptyPasswordError", "Please enter a password."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);

                App.Settings.AutologinEnabled = false;
                IsAutoLogin = false;

                Reactivate();
                return;
            }

            username = username.Replace(" ", string.Empty); // Remove whitespace

            if (Repository.Ffxiv.GetVer(App.Settings.GamePath) == Constants.BASE_GAME_VERSION &&
                App.Settings.UniqueIdCacheEnabled)
            {
                CustomMessageBox.Show(
                    Loc.Localize("UidCacheInstallError",
                        "You enabled the UID cache in the patcher settings.\nThis setting does not allow you to reinstall FFXIV.\n\nIf you want to reinstall FFXIV, please take care to disable it first."),
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                Reactivate();
                return;
            }

            var hasValidCache = CommonUniqueIdCache.Instance.HasValidCache(username) && App.Settings.UniqueIdCacheEnabled;

            var otp = string.Empty;
            if (IsOtp && (!hasValidCache || isRepair))
                otp = AskForOtp();

            if (otp == null)
            {
                Reactivate();
                return;
            }

            PersistAccount(username, password);

            if (!doingAutoLogin) App.Settings.AutologinEnabled = IsAutoLogin;

            await LoginToGame(username, password, otp, isSteam, startGame, isRepair, forceNoDalamud);
        }

        private void ShowInternetError()
        {
            CustomMessageBox.Show(
                Loc.Localize("LoginWebExceptionContent",
                    "XIVLauncher could not establish a connection to the game servers.\n\nThis may be a temporary issue, or a problem with your internet connection. Please try again later."),
                Loc.Localize("LoginNoOauthTitle", "Login issue"), MessageBoxButton.OK, MessageBoxImage.Error);

            Reactivate();
        }

        private async Task LoginToGame(string username, string password, string otp, bool isSteam, bool startGame, bool isRepair, bool forceNoDalamud)
        {
            Log.Information("LoginToGame() called");

            var gateStatus = false;
            try
            {
                gateStatus = await _launcher.GetGateStatus();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not obtain gate status");
                ShowInternetError();

                return;
            }

            try
            {
                var enableUidCache = App.Settings.UniqueIdCacheEnabled;
                var gamePath = App.Settings.GamePath;

                if (isRepair)
                    enableUidCache = false;

                var loginResult = await _launcher.Login(username, password, otp, isSteam, enableUidCache, gamePath, isRepair);

                Debug.Assert(loginResult != null, "ASSERTION FAILED loginResult != null!");

                if (loginResult.State != Launcher.LoginState.Ok)
                {
                    Log.Verbose(
                        $"[LR] {loginResult.State} {loginResult.PendingPatches != null} {loginResult.OauthLogin?.Playable}");

                    if (isRepair && loginResult.State == Launcher.LoginState.NeedsPatchGame)
                    {
                        await RepairGame(loginResult);

                        Reactivate();
                        return;
                    }

                    if (loginResult.State == Launcher.LoginState.NoService)
                    {
                        CustomMessageBox.Show(
                            Loc.Localize("LoginNoServiceMessage",
                                "This Square Enix account cannot play FINAL FANTASY XIV.\n\nIf you bought FINAL FANTASY XIV on Steam, make sure to check the \"Use Steam service account\" checkbox while logging in.\nIf Auto-Login is enabled, hold shift while starting to access settings."),
                            "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error, showHelpLinks: false, showDiscordLink: false);

                        Reactivate();
                        return;
                    }

                    if (loginResult.State == Launcher.LoginState.NoTerms)
                    {
                        CustomMessageBox.Show(
                            Loc.Localize("LoginAcceptTermsMessage",
                                "Please accept the FINAL FANTASY XIV Terms of Use in the official launcher."),
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        Reactivate();
                        return;
                    }

                    /*
                     * The server requested us to patch Boot, even though in order to get to this code, we just checked for boot patches.
                     *
                     * This means that something or someone modified boot binaries without our involvement.
                     * We have no way to go back to a "known" good state other than to do a full reinstall.
                     *
                     * This has happened multiple times with users that have viruses that infect other EXEs and change their hashes, causing the update
                     * server to reject our boot hashes.
                     *
                     * In the future we may be able to just delete /boot and run boot patches again, but this doesn't happen often enough to warrant the
                     * complexity and if boot is fucked game probably is too.
                     */
                    if (loginResult.State == Launcher.LoginState.NeedsPatchBoot)
                    {
                        CustomMessageBox.Show(
                            Loc.Localize("EverythingIsFuckedMessage",
                                "Certain essential game files were modified/broken by a third party and the game can neither update nor start.\nYou have to reinstall the game to continue.\n\nIf this keeps happening, please contact us via Discord."),
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                        Reactivate();
                        return;
                    }

                    var patchSuccess = true;
                    if (App.Settings.AskBeforePatchInstall.HasValue && App.Settings.AskBeforePatchInstall.Value)
                    {
                        var selfPatchAsk = MessageBox.Show(
                            Loc.Localize("PatchInstallDisclaimer",
                                "A new patch has been found that needs to be installed before you can play.\nDo you wish for XIVLauncher to install it?"),
                            "Out of date", MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (selfPatchAsk == MessageBoxResult.Yes)
                        {
                            patchSuccess = await InstallGamePatch(loginResult);
                        }
                        else
                        {
                            Reactivate();
                            return;
                        }
                    }
                    else
                    {
                        patchSuccess = await InstallGamePatch(loginResult);
                    }

                    if (!patchSuccess)
                    {
                        Log.Error("patchSuccess != true");
                        return;
                    }
                }

                if (startGame)
                {
                    Hide();
                    Task.Run(() => StartGameAndAddon(loginResult, gateStatus, isSteam, forceNoDalamud)).Wait();
                }
                else
                {
                    CustomMessageBox.Show(
                        Loc.Localize("LoginNoStartOk",
                            "An update check was executed and any pending updates were installed."), "XIVLauncher",
                        MessageBoxButton.OK, MessageBoxImage.Information, showHelpLinks: false, showDiscordLink: false);

                    Reactivate();
                }

            }
            catch (AggregateException ex)
            {
                //NOTE(goat): This HAS to handle all possible exceptions from StartGameAndAddon!!!!!
                foreach (var aggregate in ex.Flatten().InnerExceptions)
                {
                    switch (aggregate)
                    {
                        case Win32Exception win32Exception:
                            CustomMessageBox.Show(
                                string.Format(
                                    Loc.Localize("NativeLauncherError",
                                        "Could not start the game correctly. Please report this error.\n\nHRESULT: 0x{0}"),
                                    win32Exception.HResult.ToString("X")), "XIVLauncher Error", MessageBoxButton.OK,
                                MessageBoxImage.Error);

                            Log.Error(ex, $"NativeLauncher error; {win32Exception.HResult}: {win32Exception.Message}");
                            Reactivate();
                            break;
                        case GameExitedException:
                        {
                            Log.Error("Game exited prematurely!");

                            if (Process.GetProcessesByName("ffxiv_dx11").Length +
                                Process.GetProcessesByName("ffxiv").Length >= 2)
                            {
                                CustomMessageBox.Show(
                                    Loc.Localize("MultiboxDeniedWarning",
                                        "You can't launch more than two instances of the game by default.\n\nPlease check if there is an instance of the game that did not close correctly."),
                                    "XIVLauncher Error", image: MessageBoxImage.Error);
                            }
                            else
                            {
                                CustomMessageBox.Show(
                                    Loc.Localize("GameExitedPrematurelyError",
                                        "XIVLauncher could not detect that the game started correctly.\n\nThis may be a temporary issue. Please try restarting your PC. It is possible that your game installation is not valid."),
                                    Loc.Localize("LoginNoOauthTitle", "Login issue"), MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            }

                            Reactivate();
                            break;
                        }
                        case BinaryNotPresentException binaryNotPresentException:
                        {
                            CustomMessageBox.Show(
                                Loc.Localize("BinaryNotPresentError",
                                    "Could not find the game executable.\nThis might be caused by your antivirus. You may have to reinstall the game."),
                                "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                            Log.Error("Game binary at {0} wasn't present.", binaryNotPresentException.Path);
                            break;
                        }
                        default:
                        {
                            Log.Error(aggregate, "Unhandled AggregateException Inner during StartGameAndAddon...");

                            ErrorWindow.Show(aggregate,
                                Loc.Localize("GenericLoginError",
                                    "Error occurred during login, please report this error."), "StartGameAndAddon");
                            Environment.Exit(1);
                            break;
                        }
                    }
                }
            }
            catch (InvalidResponseException ex)
            {
                Log.Error(ex, "Received invalid server response");

                CustomMessageBox.Show(
                    Loc.Localize("LoginGenericServerIssue",
                        "The server has sent an invalid response. This is known to occur during outages or when servers are under heavy load.\nPlease wait a minute and try again, or try using the official launcher.\n\nYou can learn more about outages on the Lodestone."),
                    Loc.Localize("LoginNoOauthTitle", "Login issue"),
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Reactivate();
            }
            catch (OauthLoginException oauthLoginException)
            {
                var failedOauthMessage = oauthLoginException.Message.Replace("\\r\\n", "\n").Replace("\r\n", "\n");
                if (App.Settings.AutologinEnabled)
                {
                    failedOauthMessage +=
                        Loc.Localize("LoginNoOauthAutologinHint", "\n\nAuto-Login has been disabled.");
                    App.Settings.AutologinEnabled = false;
                }

                CustomMessageBox.Show(failedOauthMessage, Loc.Localize("LoginNoOauthTitle", "Login issue"),
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Reactivate();
            }
            catch (HttpRequestException httpException)
            {
                Log.Error(httpException, "HttpRequestException during login!");

                ShowInternetError();
            }
            catch (TaskCanceledException tce) // This usually indicates a timeout
            {
                Log.Error(tce, "TaskCanceledException during login!");

                ShowInternetError();
            }
            catch (WebException webException)
            {
                Log.Error(webException, "WebException during login!");

                ShowInternetError();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "StartGame failed...");

                if (!gateStatus)
                {
                    Log.Information("GateStatus is false.");
                    CustomMessageBox.Show(
                        Loc.Localize("MaintenanceNotice", "Maintenance seems to be in progress. The game shouldn't be launched."),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation, false, false);

                    IsLoggingIn = false;

                    Reactivate();
                    return;
                }

                ErrorWindow.Show(ex, "Please also check your login information or try again.", "Login");
                Reactivate();
            }
        }

        private async Task<bool> RepairGame(Launcher.LoginResult loginResult)
        {
            var mutex = new Mutex(false, "XivLauncherIsPatching");
            if (mutex.WaitOne(0, false))
            {
                Debug.Assert(loginResult.PendingPatches != null, "loginResult.PendingPatches != null ASSERTION FAILED");
                Debug.Assert(loginResult.PendingPatches.Length != 0, "loginResult.PendingPatches.Length != 0 ASSERTION FAILED");

                Log.Information("STARTING REPAIR");

                using var verify = new PatchVerifier(CommonSettings.Instance, loginResult, 20);

                Hide();
                IsEnabled = false;

                try
                {
                    await verify.GetPatchMeta();
                }
                catch (NoVersionReferenceException ex)
                {
                    Log.Error(ex, "No version reference found");

                    CustomMessageBox.Show(
                        Loc.Localize("NoVersionReferenceError",
                            "The version of the game you are on cannot be repaired by XIVLauncher yet, as reference information is not yet available.\nPlease try again later."),
                        Loc.Localize("LoginNoOauthTitle", "Login issue"), MessageBoxButton.OK, MessageBoxImage.Error);

                    Reactivate();
                    return false;
                }

                var progressDialog = GameRepairProgressWindowFactory(verify);
                progressDialog.Show();

                verify.Start();

                while (verify.State != PatchVerifier.VerifyState.Done && verify.State != PatchVerifier.VerifyState.Cancelled && verify.State != PatchVerifier.VerifyState.Error)
                {
                    await Task.Delay(1000);
                }

                progressDialog.Dispatcher.Invoke(() =>
                {
                    progressDialog.StopTimer();
                    progressDialog.Hide();
                    progressDialog.Close();
                });

                switch (verify.State)
                {
                    case PatchVerifier.VerifyState.Done:
                    {
                        var successMsgTemplate = Loc.Localize("GameRepairSuccess",
                            "Game files were verified by XIVLauncher. {0} {1} repaired.\n\nPlease log in normally.");

                        CustomMessageBox.Show(string.Format(successMsgTemplate, verify.NumBrokenFiles, verify.NumBrokenFiles == 1 ? Loc.Localize("GameRepairSuccessFileWas", "file was") : Loc.Localize("GameRepairSuccessFilesWere", "files were")),
                            "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }
                    case PatchVerifier.VerifyState.Error:
                        CustomMessageBox.Show(Loc.Localize("GameRepairError", "An error occurred while repairing the game files.\n\nYou may have to reinstall the game."),
                            "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }

                mutex.Close();
                mutex = null;

#if DEBUG
                if (Keyboard.IsKeyDown(Key.LeftAlt))
                    return await RepairGame(loginResult);
#endif

                return verify.State == PatchVerifier.VerifyState.Done;
            }
            else
            {
                CustomMessageBox.Show(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."), "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);

                return false;
            }
        }

        private async Task<bool> InstallGamePatch(Launcher.LoginResult loginResult)
        {
            var mutex = new Mutex(false, "XivLauncherIsPatching");
            if (mutex.WaitOne(0, false))
            {
                if (Util.CheckIsGameOpen())
                {
                    CustomMessageBox.Show(
                        Loc.Localize("GameIsOpenError", "The game and/or the official launcher are open. XIVLauncher cannot patch the game if this is the case.\nPlease close the official launcher and try again."),
                        "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    Reactivate();
                    return false;
                }

                Debug.Assert(loginResult.State == Launcher.LoginState.NeedsPatchGame,
                    "loginResult.State == Launcher.LoginState.NeedsPatchGame ASSERTION FAILED");

                Debug.Assert(loginResult.PendingPatches != null, "loginResult.PendingPatches != null ASSERTION FAILED");

                var patcher = new PatchManager(CommonSettings.Instance, Repository.Ffxiv, loginResult.PendingPatches, App.Settings.GamePath, App.Settings.PatchPath, _installer, _launcher, loginResult.UniqueId);

                IsEnabled = false;
                Hide();

                var progressDialog = PatchDownloadDialogFactory(patcher);
                progressDialog.Show();

                patcher.Start();

                while (!patcher.IsDone)
                {
                    await Task.Delay(1000);
                }

                progressDialog.Dispatcher.Invoke(() =>
                {
                    progressDialog.StopTimer();
                    progressDialog.Hide();
                    progressDialog.Close();
                });

                IsEnabled = false;

                if (patcher.IsSuccess)
                {
                    _installer.Stop();
                }
                else
                {
                    Reactivate();
                }

                mutex.Close();
                mutex = null;

                return patcher.IsSuccess;
            }
            else
            {
                CustomMessageBox.Show(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."), "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);

                return false;
            }
        }

        public void StartGameAndAddon(Launcher.LoginResult loginResult, bool gateStatus, bool isSteam, bool forceNoDalamud)
        {
            if (!gateStatus)
            {
                Log.Information("GateStatus is false.");
                CustomMessageBox.Show(
                    Loc.Localize("MaintenanceNotice",
                        "Maintenance seems to be in progress. The game shouldn't be launched."),
                    "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation, false, false);

                Reactivate();
                return;
            }

            var dalamudLauncher = new DalamudLauncher(DalamudUpdater.Overlay, App.Settings.InGameAddonLoadMethod.GetValueOrDefault(DalamudLoadMethod.DllInject));
            var dalamudOk = false;
            var isDalamudEnabled = App.Settings.InGameAddonEnabled && !forceNoDalamud;
            if (isDalamudEnabled)
            {
                dalamudOk = dalamudLauncher.HoldForUpdate(App.Settings.GamePath);
            }

            // We won't do any sanity checks here anymore, since that should be handled in StartLogin
            var gameProcess = _launcher.LaunchGame(loginResult.UniqueId, loginResult.OauthLogin.Region,
                    loginResult.OauthLogin.MaxExpansion, App.Settings.SteamIntegrationEnabled,
                    isSteam, App.Settings.AdditionalLaunchArgs, App.Settings.GamePath, App.Settings.IsDx11, App.Settings.Language.GetValueOrDefault(ClientLanguage.English), App.Settings.EncryptArguments.GetValueOrDefault(false),
                    process => {
                        if (App.Settings.InGameAddonLoadMethod == DalamudLoadMethod.EntryPoint)
                        {
                            if (isDalamudEnabled && App.Settings.IsDx11 && dalamudOk)
                            {
                                dalamudLauncher.Setup(process, App.Settings);
                                dalamudLauncher.Run();
                            }
                            else
                            {
                                Log.Warning("In-Game addon was not enabled or failed to ensure (tried to load as entry point)");
                            }
                        }
                    });

            Troubleshooting.LogTroubleshooting();

            if (gameProcess == null)
            {
                Log.Information("GameProcess was null...");
                IsLoggingIn = false;
                return;
            }

            CleanUp();

            var addonMgr = new AddonManager();

            try
            {
                if (App.Settings.AddonList == null)
                    App.Settings.AddonList = new List<AddonEntry>();

                var addons = App.Settings.AddonList.Where(x => x.IsEnabled).Select(x => x.Addon).Cast<IAddon>().ToList();
                if (App.Settings.InGameAddonLoadMethod == DalamudLoadMethod.DllInject)
                {
                    if (isDalamudEnabled && App.Settings.IsDx11 && dalamudOk)
                    {
                        addons.Add(dalamudLauncher);
                    }
                    else
                    {
                        Log.Warning("In-Game addon was not enabled or failed to ensure (tried to load via DLL injection)");
                    }
                }
                addonMgr.RunAddons(gameProcess, App.Settings, addons);
            }
            catch (Exception ex)
            {
                ErrorWindow.Show(ex,
                    "This could be caused by your antivirus, please check its logs and add any needed exclusions.",
                    "Addons");
                IsLoggingIn = false;

                addonMgr.StopAddons();
            }

            var watchThread = new Thread(() =>
            {
                while (!gameProcess.HasExited)
                {
                    gameProcess.Refresh();
                    Thread.Sleep(100);
                }

                Log.Information("Game has exited.");

                if (addonMgr.IsRunning)
                    addonMgr.StopAddons();

                CleanUp();

                Environment.Exit(0);
            });
            watchThread.Start();

            Log.Debug("Started WatchThread");
        }

        public void OnWindowClosed(object sender, object args)
        {
            CleanUp();
            Application.Current.Shutdown();
        }

        public void OnWindowClosing(object sender, CancelEventArgs args)
        {
            if (IsLoggingIn)
                args.Cancel = true;
        }

        private string AskForOtp()
        {
            var otpDialog = OtpInputDialogFactory();
            otpDialog.ShowDialog();

            return otpDialog.Result;
        }

        private void PersistAccount(string username, string password)
        {
            if (AccountManager.CurrentAccount != null && AccountManager.CurrentAccount.UserName.Equals(username) &&
                AccountManager.CurrentAccount.Password != password &&
                AccountManager.CurrentAccount.SavePassword)
                AccountManager.UpdatePassword(AccountManager.CurrentAccount, password);

            if (AccountManager.CurrentAccount == null ||
                AccountManager.CurrentAccount.Id != $"{username}-{IsOtp}-{IsSteam}")
            {
                var accountToSave = new XivAccount(username)
                {
                    Password = password,
                    SavePassword = true,
                    UseOtp = IsOtp,
                    UseSteamServiceAccount = IsSteam
                };

                AccountManager.AddAccount(accountToSave);

                AccountManager.CurrentAccount = accountToSave;
            }
        }

        private void CleanUp()
        {
            Task.Run(PatchManager.UnInitializeAcquisition).Wait();
            _installer.Stop();
        }

        private void Reactivate()
        {
            IsLoggingIn = false;
            IsEnabled = true;
            LoginCardTransitionerIndex = 1;

            ReloadHeadlines();
            Activate();
        }

        private async Task<bool> HandleBootCheck()
        {
            try
            {
                if (App.Settings.PatchPath is { Exists: false })
                {
                    App.Settings.PatchPath = null;
                }

                App.Settings.PatchPath ??= new DirectoryInfo(Path.Combine(Paths.RoamingPath, "patches"));

                Common.Game.Patch.PatchList.PatchListEntry[] bootPatches = null;
                try
                {
                    bootPatches = await _launcher.CheckBootVersion(App.Settings.GamePath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unable to check boot version.");
                    MessageBox.Show(Loc.Localize("CheckBootVersionError", "XIVLauncher was not able to check the boot version for the select game installation. This can happen if a maintenance is currently in progress or if your connection to the version check server is not available. Please report this error if you are able to login with the official launcher, but not XIVLauncher."), "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);

                    Reactivate();
                    return false;
                }

                if (bootPatches == null)
                    return true;

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
                        return false;
                    }

                    var patcher = new PatchManager(CommonSettings.Instance, Repository.Boot, bootPatches, App.Settings.GamePath,
                        App.Settings.PatchPath, _installer, null, null);

                    IsEnabled = false;

                    var progressDialog = PatchDownloadDialogFactory(patcher);
                    progressDialog.Show();

                    patcher.Start();

                    while (!patcher.IsDone)
                    {
                        await Task.Delay(1000);
                    }

                    progressDialog.Dispatcher.Invoke(() =>
                    {
                        progressDialog.Hide();
                        progressDialog.Close();
                    });

                    IsEnabled = true;

                    // This is a good indicator that we should clear the UID cache
                    CommonUniqueIdCache.Instance.Reset();

                    mutex.Close();
                    mutex = null;

                    return patcher.IsSuccess;
                }

                CustomMessageBox.Show(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."), "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);

                return false;
            }
            catch (Exception ex)
            {
                ErrorWindow.Show(ex, "Could not patch boot.", nameof(HandleBootCheck));
                Environment.Exit(0);

                return false;
            }
        }

        #region Commands

        public ICommand StartLoginCommand { get; set; }

        public ICommand LoginNoStartCommand { get; set; }

        public ICommand LoginNoDalamudCommand { get; set; }

        public ICommand LoginRepairCommand { get; set; }

        #endregion

        #region Bindings

        private bool _isAutoLogin;
        public bool IsAutoLogin
        {
            get => _isAutoLogin;
            set
            {
                _isAutoLogin = value;
                OnPropertyChanged(nameof(IsAutoLogin));
            }
        }

        private bool _isOtp;
        public bool IsOtp
        {
            get => _isOtp;
            set
            {
                _isOtp = value;
                OnPropertyChanged(nameof(IsOtp));
            }
        }

        private bool _isSteam;
        public bool IsSteam
        {
            get => _isSteam;
            set
            {
                _isSteam = value;
                OnPropertyChanged(nameof(IsSteam));
            }
        }

        private string _username;
        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        private int _loginCardTransitionerIndex;
        public int LoginCardTransitionerIndex
        {
            get => _loginCardTransitionerIndex;
            set
            {
                _loginCardTransitionerIndex = value;
                OnPropertyChanged(nameof(LoginCardTransitionerIndex));
            }
        }

        private bool _isLoadingDialogOpen;
        public bool IsLoadingDialogOpen
        {
            get => _isLoadingDialogOpen;
            set
            {
                _isLoadingDialogOpen = value;
                OnPropertyChanged(nameof(IsLoadingDialogOpen));
            }
        }

        private Visibility _loadingDialogCancelButtonVisibility;
        public Visibility LoadingDialogCancelButtonVisibility
        {
            get => _loadingDialogCancelButtonVisibility;
            set
            {
                _loadingDialogCancelButtonVisibility = value;
                OnPropertyChanged(nameof(LoadingDialogCancelButtonVisibility));
            }
        }

        private string _loadingDialogMessage;
        public string LoadingDialogMessage
        {
            get => _loadingDialogMessage;
            set
            {
                _loadingDialogMessage = value;
                OnPropertyChanged(nameof(LoadingDialogMessage));
            }
        }

        #endregion

        #region Localization

        private void SetupLoc()
        {
            LoginUsernameLoc = Loc.Localize("LoginBoxUsername", "Square Enix ID");
            LoginPasswordLoc = Loc.Localize("LoginBoxPassword", "Password");
            AutoLoginLoc = Loc.Localize("LoginBoxAutoLogin", "Log in automatically");
            OtpLoc = Loc.Localize("LoginBoxOtp", "Use One-Time-Passwords");
            SteamLoc = Loc.Localize("LoginBoxSteam", "Use Steam service account");
            LoginLoc = Loc.Localize("LoginBoxLogin", "Log in");
            LoginNoStartLoc = Loc.Localize("LoginBoxNoStartLogin", "Update without starting");
            LoginRepairLoc = Loc.Localize("LoginBoxRepairLogin", "Repair game files");
            LoginNoDalamudLoc = Loc.Localize("LoginBoxNoDalamudLogin", "Start without Dalamud");
            LoginTooltipLoc = Loc.Localize("LoginBoxLoginTooltip", "Log in with the provided credentials");
            WaitingForMaintenanceLoc = Loc.Localize("LoginBoxWaitingForMaint", "Waiting for maintenance to be over...");
            CancelLoc = Loc.Localize("Cancel", "Cancel");
            OpenAccountSwitcherLoc = Loc.Localize("OpenAccountSwitcher", "Open Account Switcher");
            SettingsLoc = Loc.Localize("Settings", "Settings");
            WorldStatusLoc = Loc.Localize("WorldStatus", "World Status");
            MaintenanceQueue = Loc.Localize("MaintenanceQueue", "Wait for maintenance to be over");
            IsLoggingInLoc = Loc.Localize("LoadingDialogIsLoggingIn", "Logging in...");
        }

        public string LoginUsernameLoc { get; private set; }
        public string LoginPasswordLoc { get; private set; }
        public string AutoLoginLoc { get; private set; }
        public string OtpLoc { get; private set; }
        public string SteamLoc { get; private set; }
        public string LoginLoc { get; private set; }
        public string LoginNoStartLoc { get; private set; }
        public string LoginNoDalamudLoc { get; private set; }
        public string LoginRepairLoc { get; private set; }
        public string WaitingForMaintenanceLoc { get; private set; }
        public string CancelLoc { get; private set; }
        public string LoginTooltipLoc { get; private set; }
        public string OpenAccountSwitcherLoc { get; private set; }
        public string SettingsLoc { get; private set; }
        public string WorldStatusLoc { get; private set; }
        public string MaintenanceQueue { get; private set; }
        public string IsLoggingInLoc { get; private set; }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}