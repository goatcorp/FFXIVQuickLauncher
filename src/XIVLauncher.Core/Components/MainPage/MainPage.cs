using System.Diagnostics;
using ImGuiNET;
using System.Numerics;
using CheapLoc;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Addon;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Windows;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Unix.Compatibility.GameFixes;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.Accounts;
using XIVLauncher.Common.Game.Exceptions;
using XIVLauncher.Core.Support;

namespace XIVLauncher.Core.Components.MainPage;

public class MainPage : Page
{
    private readonly LoginFrame loginFrame;
    private readonly NewsFrame newsFrame;
    private readonly ActionButtons actionButtons;

    public bool IsLoggingIn { get; private set; }

    public MainPage(LauncherApp app)
        : base(app)
    {
        this.loginFrame = new LoginFrame(this);
        this.newsFrame = new NewsFrame(app);

        this.actionButtons = new ActionButtons();

        this.AccountSwitcher = new AccountSwitcher(app.Accounts);
        this.AccountSwitcher.AccountChanged += this.AccountSwitcherOnAccountChanged;

        this.loginFrame.OnLogin += this.ProcessLogin;
        this.actionButtons.OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings;

        this.Padding = new Vector2(32f, 32f);

        var savedAccount = App.Accounts.CurrentAccount;

        if (savedAccount != null) this.SwitchAccount(savedAccount, false);

        if (PlatformHelpers.IsElevated())
            App.ShowMessage("XIVLauncher is running as administrator/root user.\nThis can cause various issues, including but not limited to addons failing to launch and hotkey applications failing to respond.\n\nPlease take care to avoid running XIVLauncher with elevated privileges", "XIVLauncher");

        Troubleshooting.LogTroubleshooting();
    }

    public AccountSwitcher AccountSwitcher { get; private set; }

    public void DoAutoLoginIfApplicable()
    {
        Debug.Assert(App.State == LauncherApp.LauncherState.Main);

        if ((App.Settings.IsAutologin ?? false) && !string.IsNullOrEmpty(this.loginFrame.Username) && !string.IsNullOrEmpty(this.loginFrame.Password))
            ProcessLogin(LoginAction.Game);
    }

    public override void Draw()
    {
        base.Draw();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(32f, 32f));
        this.newsFrame.Draw();

        ImGui.SameLine();

        this.AccountSwitcher.Draw();
        this.loginFrame.Draw();

        this.actionButtons.Draw();
    }

    public void ReloadNews() => this.newsFrame.ReloadNews();

    private void SwitchAccount(XivAccount account, bool saveAsCurrent)
    {
        this.loginFrame.Username = account.UserName;
        this.loginFrame.IsOtp = account.UseOtp;
        this.loginFrame.IsSteam = account.UseSteamServiceAccount;
        this.loginFrame.IsAutoLogin = App.Settings.IsAutologin ?? false;

        if (account.SavePassword)
            this.loginFrame.Password = account.Password;

        if (saveAsCurrent)
        {
            App.Accounts.CurrentAccount = account;
        }
    }

    private void AccountSwitcherOnAccountChanged(object? sender, XivAccount e)
    {
        SwitchAccount(e, true);
    }

    private void ProcessLogin(LoginAction action)
    {
        if (this.IsLoggingIn)
            return;

        this.App.StartLoading("Logging in...", canDisableAutoLogin: true);

        // if (Program.UsesFallbackSteamAppId && this.loginFrame.IsSteam)
        //     throw new Exception("Doesn't own Steam AppId on this account.");

        Task.Run(async () =>
        {
            if (GameHelpers.CheckIsGameOpen() && action == LoginAction.Repair)
            {
                App.ShowMessageBlocking("The game and/or the official launcher are open. XIVLauncher cannot repair the game if this is the case.\nPlease close them and try again.", "XIVLauncher");

                Reactivate();
                return;
            }

            if (Repository.Ffxiv.GetVer(App.Settings.GamePath) == Constants.BASE_GAME_VERSION &&
                App.Settings.IsUidCacheEnabled == true)
            {
                App.ShowMessageBlocking(
                    "You enabled the UID cache in the patcher settings.\nThis setting does not allow you to reinstall FFXIV.\n\nIf you want to reinstall FFXIV, please take care to disable it first.",
                    "XIVLauncher Error");

                this.Reactivate();
                return;
            }

            IsLoggingIn = true;

            App.Settings.IsAutologin = this.loginFrame.IsAutoLogin;

            var result = await Login(loginFrame.Username, loginFrame.Password, loginFrame.IsOtp, loginFrame.IsSteam, false, action).ConfigureAwait(false);

            if (result)
            {
                Environment.Exit(0);
            }
            else
            {
                Log.Verbose("Reactivated after Login() != true");
                this.Reactivate();
            }
        }).ContinueWith(t =>
        {
            if (!App.HandleContinuationBlocking(t))
                this.Reactivate();
        });
    }

    public async Task<bool> Login(string username, string password, bool isOtp, bool isSteam, bool doingAutoLogin, LoginAction action)
    {
        if (action == LoginAction.Fake)
        {
            IGameRunner gameRunner;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                gameRunner = new WindowsGameRunner(null, false, Program.DalamudUpdater.Runtime);
            else
                gameRunner = new UnixGameRunner(Program.CompatibilityTools, null, false);

            App.Launcher.LaunchGame(gameRunner, "0", 1, 2, false, "", App.Settings.GamePath!, true, ClientLanguage.Japanese, true, DpiAwareness.Unaware);

            return false;
        }

        var bootRes = await HandleBootCheck().ConfigureAwait(false);

        if (!bootRes)
            return false;

        var otp = string.Empty;

        if (isOtp)
        {
            App.AskForOtp();
            otp = App.WaitForOtp();

            // Make sure we are loading again
            App.State = LauncherApp.LauncherState.Loading;
            Program.Invalidate(10);
        }

        if (otp == null)
            return false;

        PersistAccount(username, password, isOtp, isSteam);

        var loginResult = await TryLoginToGame(username, password, otp, isSteam, action).ConfigureAwait(false);

        return await TryProcessLoginResult(loginResult, isSteam, action).ConfigureAwait(false);
    }

    private async Task<Launcher.LoginResult> TryLoginToGame(string username, string password, string otp, bool isSteam, LoginAction action)
    {
        bool? gateStatus = null;

#if !DEBUG
        try
        {
            // TODO: Also apply the login status fix here
            var gate = await App.Launcher.GetGateStatus(App.Settings.ClientLanguage ?? ClientLanguage.English).ConfigureAwait(false);
            gateStatus = gate.Status;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not obtain gate status");
        }

        if (gateStatus == null)
        {
            /*
            CustomMessageBox.Builder.NewFrom(Loc.Localize("GateUnreachable", "The login servers could not be reached. This usually indicates that the game is under maintenance, or that your connection to the login servers is unstable.\n\nPlease try again later."))
                            .WithImage(MessageBoxImage.Asterisk)
                            .WithButtons(MessageBoxButton.OK)
                            .WithShowHelpLinks(true)
                            .WithCaption("XIVLauncher")
                            .WithParentWindow(_window)
                            .Show();
                            */

            App.ShowMessageBlocking("Login servers could not be reached or maintenance is in progress. This might be a problem with your connection.");

            return null;
        }

        if (gateStatus == false)
        {
            /*
            CustomMessageBox.Builder.NewFrom(Loc.Localize("GateClosed", "FFXIV is currently under maintenance. Please try again later or see official sources for more information."))
                            .WithImage(MessageBoxImage.Asterisk)
                            .WithButtons(MessageBoxButton.OK)
                            .WithCaption("XIVLauncher")
                            .WithParentWindow(_window)
                            .Show();*/

            App.ShowMessageBlocking("Maintenance is in progress.");

            return null;
        }
#endif

        try
        {
            var enableUidCache = App.Settings.IsUidCacheEnabled ?? false;
            var gamePath = App.Settings.GamePath;

            if (action == LoginAction.Repair)
                return await App.Launcher.Login(username, password, otp, isSteam, false, gamePath, true, App.Settings.IsFt.GetValueOrDefault(false)).ConfigureAwait(false);
            else
                return await App.Launcher.Login(username, password, otp, isSteam, enableUidCache, gamePath, false, App.Settings.IsFt.GetValueOrDefault(false)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not login to game");
            throw;
        }
    }

    private async Task<bool> TryProcessLoginResult(Launcher.LoginResult loginResult, bool isSteam, LoginAction action)
    {
        if (loginResult.State == Launcher.LoginState.NoService)
        {
            throw new Exception("No service account or subscription");

            return false;
        }

        if (loginResult.State == Launcher.LoginState.NoTerms)
        {
            throw new Exception("Need to accept terms of use");

            return false;
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
            /*
            CustomMessageBox.Show(
                Loc.Localize("EverythingIsFuckedMessage",
                    "Certain essential game files were modified/broken by a third party and the game can neither update nor start.\nYou have to reinstall the game to continue.\n\nIf this keeps happening, please contact us via Discord."),
                "Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: _window);
                */

            throw new Exception("Boot conflict, need reinstall");

            return false;
        }

        if (action == LoginAction.Repair)
        {
            try
            {
                if (loginResult.State == Launcher.LoginState.NeedsPatchGame)
                {
                    if (!await RepairGame(loginResult).ConfigureAwait(false))
                        return false;

                    loginResult.State = Launcher.LoginState.Ok;
                }
                else
                {
                    /*
                    CustomMessageBox.Show(
                        Loc.Localize("LoginRepairResponseIsNotNeedsPatchGame",
                            "The server sent an incorrect response - the repair cannot proceed."),
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: _window);
                        */
                    throw new Exception("Repair login state not NeedsPatchGame");

                    return false;
                }
            }
            catch (Exception ex)
            {
                /*
                 * We should never reach here.
                 * If server responds badly, then it should not even have reached this point, as error cases should have been handled before.
                 * If RepairGame was unsuccessful, then it should have handled all of its possible errors, instead of propagating it upwards.
                 */
                //CustomMessageBox.Builder.NewFrom(ex, "TryProcessLoginResult/Repair").WithParentWindow(_window).Show();
                throw;

                return false;
            }
        }

        if (loginResult.State == Launcher.LoginState.NeedsPatchGame)
        {
            if (!await InstallGamePatch(loginResult).ConfigureAwait(false))
            {
                Log.Error("patchSuccess != true");
                return false;
            }

            loginResult.State = Launcher.LoginState.Ok;
        }

        if (action == LoginAction.GameNoLaunch)
        {
            App.ShowMessageBlocking(
                Loc.Localize("LoginNoStartOk",
                    "An update check was executed and any pending updates were installed."), "XIVLauncher");

            return false;
        }

        Debug.Assert(loginResult.State == Launcher.LoginState.Ok);

        while (true)
        {
            List<Exception> exceptions = new();

            try
            {
                using var process = await StartGameAndAddon(loginResult, isSteam, action == LoginAction.GameNoDalamud, false).ConfigureAwait(false);

                if (process is null)
                    throw new Exception("Could not obtain Process Handle");

                if (process.ExitCode != 0 && (App.Settings.TreatNonZeroExitCodeAsFailure ?? false))
                {
                    throw new Exception("Game exited with non-zero exit code");

                    /*
                    switch (new CustomMessageBox.Builder()
                            .WithTextFormatted(
                                Loc.Localize("LaunchGameNonZeroExitCode",
                                    "It looks like the game has exited with a fatal error. Do you want to relaunch the game?\n\nExit code: 0x{0:X8}"),
                                (uint)process.ExitCode)
                            .WithImage(MessageBoxImage.Exclamation)
                            .WithShowHelpLinks(true)
                            .WithShowDiscordLink(true)
                            .WithShowNewGitHubIssue(true)
                            .WithButtons(MessageBoxButton.YesNoCancel)
                            .WithDefaultResult(MessageBoxResult.Yes)
                            .WithCancelResult(MessageBoxResult.No)
                            .WithYesButtonText(Loc.Localize("LaunchGameRelaunch", "_Relaunch"))
                            .WithNoButtonText(Loc.Localize("LaunchGameClose", "_Close"))
                            .WithCancelButtonText(Loc.Localize("LaunchGameDoNotAskAgain", "_Don't ask again"))
                            .WithParentWindow(_window)
                            .Show())
                    {
                        case MessageBoxResult.Yes:
                            continue;

                        case MessageBoxResult.No:
                            return true;

                        case MessageBoxResult.Cancel:
                            App.Settings.TreatNonZeroExitCodeAsFailure = false;
                            return true;
                    }
                    */
                }

                return true;
            }
            /*
            catch (AggregateException ex)
            {
                Log.Error(ex, "StartGameAndError resulted in one or more exceptions.");

                exceptions.Add(ex.Flatten().InnerException);
            }
            */
            catch (Exception ex)
            {
                Log.Error(ex, "StartGameAndError resulted in an exception.");

                exceptions.Add(ex);
                throw;
            }

            /*
            var builder = new CustomMessageBox.Builder()
                          .WithImage(MessageBoxImage.Error)
                          .WithShowHelpLinks(true)
                          .WithShowDiscordLink(true)
                          .WithShowNewGitHubIssue(true)
                          .WithButtons(MessageBoxButton.YesNo)
                          .WithDefaultResult(MessageBoxResult.No)
                          .WithCancelResult(MessageBoxResult.No)
                          .WithYesButtonText(Loc.Localize("LaunchGameRetry", "_Try again"))
                          .WithNoButtonText(Loc.Localize("LaunchGameClose", "_Close"))
                          .WithParentWindow(_window);

            //NOTE(goat): This HAS to handle all possible exceptions from StartGameAndAddon!!!!!
            List<string> summaries = new();
            List<string> actionables = new();
            List<string> descriptions = new();

            foreach (var exception in exceptions)
            {
                switch (exception)
                {
                    case GameExitedException:
                        var count = 0;

                        foreach (var processName in new string[] { "ffxiv_dx11", "ffxiv" })
                        {
                            foreach (var process in Process.GetProcessesByName(processName))
                            {
                                count++;
                                process.Dispose();
                            }
                        }

                        if (count >= 2)
                        {
                            summaries.Add(Loc.Localize("MultiboxDeniedWarningSummary",
                                "You can't launch more than two instances of the game by default."));
                            actionables.Add(string.Format(
                                Loc.Localize("MultiboxDeniedWarningActionable",
                                    "Please check if there is an instance of the game that did not close correctly. (Detected: {0})"),
                                count));
                            descriptions.Add(null);

                            builder.WithButtons(MessageBoxButton.YesNoCancel)
                                   .WithDefaultResult(MessageBoxResult.Yes)
                                   .WithCancelButtonText(Loc.Localize("LaunchGameKillThenRetry", "_Kill then try again"));
                        }
                        else
                        {
                            summaries.Add(Loc.Localize("GameExitedPrematurelyErrorSummary",
                                "XIVLauncher could not detect that the game started correctly."));
                            actionables.Add(Loc.Localize("GameExitedPrematurelyErrorActionable",
                                "This may be a temporary issue. Please try restarting your PC. It is possible that your game installation is not valid."));
                            descriptions.Add(null);
                        }

                        break;

                    case BinaryNotPresentException:
                        summaries.Add(Loc.Localize("BinaryNotPresentErrorSummary",
                            "Could not find the game executable."));
                        actionables.Add(Loc.Localize("BinaryNotPresentErrorActionable",
                            "This might be caused by your antivirus. You may have to reinstall the game."));
                        descriptions.Add(null);
                        break;

                    case IOException:
                        summaries.Add(Loc.Localize("LoginIoErrorSummary",
                            "Could not locate game data files."));
                        summaries.Add(Loc.Localize("LoginIoErrorActionable",
                            "This may mean that the game path set in XIVLauncher isn't preset, e.g. on a disconnected drive or network storage. Please check the game path in the XIVLauncher settings."));
                        descriptions.Add(exception.ToString());
                        break;

                    case Win32Exception win32Exception:
                        summaries.Add(string.Format(
                            Loc.Localize("UnexpectedErrorSummary",
                                "Unexpected error has occurred. ({0})"),
                            $"0x{(uint)win32Exception.HResult:X8}: {win32Exception.Message}"));
                        actionables.Add(Loc.Localize("UnexpectedErrorActionable",
                            "Please report this error."));
                        descriptions.Add(exception.ToString());
                        break;

                    default:
                        summaries.Add(string.Format(
                            Loc.Localize("UnexpectedErrorSummary",
                                "Unexpected error has occurred. ({0})"),
                            exception.Message));
                        actionables.Add(Loc.Localize("UnexpectedErrorActionable",
                            "Please report this error."));
                        descriptions.Add(exception.ToString());
                        break;
                }
            }

            if (exceptions.Count == 1)
            {
                builder.WithText($"{summaries[0]}\n\n{actionables[0]}")
                       .WithDescription(descriptions[0]);
            }
            else
            {
                builder.WithText(Loc.Localize("MultipleErrors", "Multiple errors have occurred."));

                for (var i = 0; i < summaries.Count; i++)
                {
                    builder.WithAppendText($"\n{i + 1}. {summaries[i]}\n    => {actionables[i]}");
                    if (string.IsNullOrWhiteSpace(descriptions[i]))
                        continue;
                    builder.WithAppendDescription($"########## Exception {i + 1} ##########\n{descriptions[i]}\n\n");
                }
            }

            if (descriptions.Any(x => x != null))
                builder.WithAppendSettingsDescription("Login");


            switch (builder.Show())
            {
                case MessageBoxResult.Yes:
                    continue;

                case MessageBoxResult.No:
                    return false;

                case MessageBoxResult.Cancel:
                    for (var pass = 0; pass < 8; pass++)
                    {
                        var allKilled = true;

                        foreach (var processName in new string[] { "ffxiv_dx11", "ffxiv" })
                        {
                            foreach (var process in Process.GetProcessesByName(processName))
                            {
                                allKilled = false;

                                try
                                {
                                    process.Kill();
                                }
                                catch (Exception ex2)
                                {
                                    Log.Warning(ex2, "Could not kill process (PID={0}, name={1})", process.Id, process.ProcessName);
                                }
                                finally
                                {
                                    process.Dispose();
                                }
                            }
                        }

                        if (allKilled)
                            break;
                    }

                    Task.Delay(1000).Wait();
                    continue;
            }
            */
        }
    }

    public async Task<Process> StartGameAndAddon(Launcher.LoginResult loginResult, bool isSteam, bool forceNoDalamud, bool noThird)
    {
        var dalamudOk = false;

        IDalamudRunner dalamudRunner;
        IDalamudCompatibilityCheck dalamudCompatCheck;

        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                dalamudRunner = new WindowsDalamudRunner();
                dalamudCompatCheck = new WindowsDalamudCompatibilityCheck();
                break;

            case PlatformID.Unix:
                dalamudRunner = new UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime);
                dalamudCompatCheck = new UnixDalamudCompatibilityCheck();
                break;

            default:
                throw new NotImplementedException();
        }

        Troubleshooting.LogTroubleshooting();

        var dalamudLauncher = new DalamudLauncher(dalamudRunner, Program.DalamudUpdater, App.Settings.DalamudLoadMethod.GetValueOrDefault(DalamudLoadMethod.DllInject),
            App.Settings.GamePath, App.Storage.Root, App.Settings.ClientLanguage ?? ClientLanguage.English, App.Settings.DalamudLoadDelay, false, false, noThird,
            Troubleshooting.GetTroubleshootingJson());

        try
        {
            dalamudCompatCheck.EnsureCompatibility();
        }
        catch (IDalamudCompatibilityCheck.NoRedistsException ex)
        {
            Log.Error(ex, "No Dalamud Redists found");

            throw;
            /*
            CustomMessageBox.Show(
                Loc.Localize("DalamudVc2019RedistError",
                    "The XIVLauncher in-game addon needs the Microsoft Visual C++ 2015-2019 redistributable to be installed to continue. Please install it from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation, parentWindow: _window);
                */
        }
        catch (IDalamudCompatibilityCheck.ArchitectureNotSupportedException ex)
        {
            Log.Error(ex, "Architecture not supported");

            throw;
            /*
            CustomMessageBox.Show(
                Loc.Localize("DalamudArchError",
                    "Dalamud cannot run your computer's architecture. Please make sure that you are running a 64-bit version of Windows.\nIf you are using Windows on ARM, please make sure that x64-Emulation is enabled for XIVLauncher."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation, parentWindow: _window);
                */
        }

        if (App.Settings.DalamudEnabled.GetValueOrDefault(true) && !forceNoDalamud && App.Settings.IsDx11.GetValueOrDefault(true))
        {
            try
            {
                App.StartLoading("Waiting for Dalamud to be ready...", "This may take a little while. Please hold!");
                dalamudOk = dalamudLauncher.HoldForUpdate(App.Settings.GamePath) == DalamudLauncher.DalamudInstallState.Ok;
            }
            catch (DalamudRunnerException ex)
            {
                Log.Error(ex, "Couldn't ensure Dalamud runner");

                var runnerErrorMessage = Loc.Localize("DalamudRunnerError",
                    "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".");

                throw;
                /*
                CustomMessageBox.Builder
                                .NewFrom(runnerErrorMessage)
                                .WithImage(MessageBoxImage.Error)
                                .WithButtons(MessageBoxButton.OK)
                                .WithShowHelpLinks()
                                .WithParentWindow(_window)
                                .Show();
                                */
            }
        }

        IGameRunner runner;

        var gameArgs = App.Settings.AdditionalArgs ?? string.Empty;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            runner = new WindowsGameRunner(dalamudLauncher, dalamudOk, Program.DalamudUpdater.Runtime);
        }
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            if (App.Settings.WineStartupType == WineStartupType.Custom && App.Settings.WineBinaryPath == null)
                throw new Exception("Custom wine binary path wasn't set.");

            var signal = new ManualResetEvent(false);
            var isFailed = false;

            if (App.Settings.WineStartupType == WineStartupType.Managed)
            {
                var _ = Task.Run(async () =>
                {
                    var tempPath = App.Storage.GetFolder("temp");

                    await Program.CompatibilityTools.EnsureTool(tempPath).ConfigureAwait(false);

                    var gameFixApply = new GameFixApply(App.Settings.GamePath, App.Settings.GameConfigPath, Program.CompatibilityTools.Settings.Prefix, tempPath);
                    gameFixApply.UpdateProgress += (text, hasProgress, progress) =>
                    {
                        App.LoadingPage.Line1 = "Applying game-specific fixes...";
                        App.LoadingPage.Line2 = text;
                        App.LoadingPage.Line3 = "This may take a little while. Please hold!";
                        App.LoadingPage.IsIndeterminate = !hasProgress;
                        App.LoadingPage.Progress = progress;
                    };

                    gameFixApply.Run();
                }).ContinueWith(t =>
                {
                    isFailed = t.IsFaulted || t.IsCanceled;

                    if (isFailed)
                        Log.Error(t.Exception, "Couldn't ensure compatibility tool");

                    signal.Set();
                });

                App.StartLoading("Ensuring compatibility tool...", "This may take a little while. Please hold!");
                signal.WaitOne();
                signal.Dispose();

                if (isFailed)
                    return null;
            }

            App.StartLoading("Starting game...", "Have fun!");

            runner = new UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk);

            // SE has its own way of encoding spaces when encrypting arguments, which interferes 
            // with quoting, but they are necessary when passing paths unencrypted
            var userPath = Program.CompatibilityTools.UnixToWinePath(App.Settings.GameConfigPath.FullName);
            if (App.Settings.IsEncryptArgs.GetValueOrDefault(true))
                gameArgs += $" UserPath={userPath}";
            else
                gameArgs += $" UserPath=\"{userPath}\"";

            gameArgs = gameArgs.Trim();
        }
        else
        {
            throw new NotImplementedException();
        }

        if (!Program.IsSteamDeckHardware)
        {
            Hide();
        }
        else
        {
            App.State = LauncherApp.LauncherState.SteamDeckPrompt;
        }

        // We won't do any sanity checks here anymore, since that should be handled in StartLogin
        var launchedProcess = App.Launcher.LaunchGame(runner,
            loginResult.UniqueId,
            loginResult.OauthLogin.Region,
            loginResult.OauthLogin.MaxExpansion,
            isSteam,
            gameArgs,
            App.Settings.GamePath,
            App.Settings.IsDx11 ?? true,
            App.Settings.ClientLanguage.GetValueOrDefault(ClientLanguage.English),
            App.Settings.IsEncryptArgs.GetValueOrDefault(true),
            App.Settings.DpiAwareness.GetValueOrDefault(DpiAwareness.Unaware));

        if (launchedProcess == null)
        {
            Log.Information("GameProcess was null...");
            IsLoggingIn = false;
            return null;
        }

        var addonMgr = new AddonManager();

        try
        {
            App.Settings.Addons ??= new List<AddonEntry>();

            var addons = App.Settings.Addons.Where(x => x.IsEnabled).Select(x => x.Addon).Cast<IAddon>().ToList();

            addonMgr.RunAddons(launchedProcess.Id, addons);
        }
        catch (Exception ex)
        {
            /*
            CustomMessageBox.Builder
                            .NewFrom(ex, "Addons")
                            .WithAppendText("\n\n")
                            .WithAppendText(Loc.Localize("AddonLoadError",
                                "This could be caused by your antivirus, please check its logs and add any needed exclusions."))
                            .WithParentWindow(_window)
                            .Show();
                            */

            IsLoggingIn = false;

            addonMgr.StopAddons();
            throw;
        }

        Log.Debug("Waiting for game to exit");

        await Task.Run(() => launchedProcess!.WaitForExit()).ConfigureAwait(false);

        Log.Verbose("Game has exited");

        if (addonMgr.IsRunning)
            addonMgr.StopAddons();

        try
        {
            if (App.Steam.IsValid)
            {
                App.Steam.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not shut down Steam");
        }

        return launchedProcess!;
    }

    private void PersistAccount(string username, string password, bool isOtp, bool isSteam)
    {
        if (App.Accounts.CurrentAccount != null && App.Accounts.CurrentAccount.UserName.Equals(username) &&
            App.Accounts.CurrentAccount.Password != password &&
            App.Accounts.CurrentAccount.SavePassword)
            App.Accounts.UpdatePassword(App.Accounts.CurrentAccount, password);

        if (App.Accounts.CurrentAccount == null ||
            App.Accounts.CurrentAccount.Id != $"{username}-{isOtp}-{isSteam}")
        {
            var accountToSave = new XivAccount(username)
            {
                Password = password,
                SavePassword = true,
                UseOtp = isOtp,
                UseSteamServiceAccount = isSteam
            };

            App.Accounts.AddAccount(accountToSave);

            App.Accounts.CurrentAccount = accountToSave;
        }
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

            PatchListEntry[] bootPatches = null;

            try
            {
                bootPatches = await App.Launcher.CheckBootVersion(App.Settings.GamePath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to check boot version");
                App.ShowMessage(
                    Loc.Localize("CheckBootVersionError",
                        "XIVLauncher was not able to check the boot version for the select game installation. This can happen if a maintenance is currently in progress or if your connection to the version check server is not available. Please report this error if you are able to login with the official launcher, but not XIVLauncher."),
                    "XIVLauncher");

                return false;
            }

            if (bootPatches == null)
                return true;

            return await TryHandlePatchAsync(Repository.Boot, bootPatches, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            App.ShowExceptionBlocking(ex, "PatchBoot");
            Environment.Exit(0);

            return false;
        }
    }

    private Task<bool> InstallGamePatch(Launcher.LoginResult loginResult)
    {
        Debug.Assert(loginResult.State == Launcher.LoginState.NeedsPatchGame,
            "loginResult.State == Launcher.LoginState.NeedsPatchGame ASSERTION FAILED");

        Debug.Assert(loginResult.PendingPatches != null, "loginResult.PendingPatches != null ASSERTION FAILED");

        return TryHandlePatchAsync(Repository.Ffxiv, loginResult.PendingPatches, loginResult.UniqueId);
    }

    private async Task<bool> TryHandlePatchAsync(Repository repository, PatchListEntry[] pendingPatches, string sid)
    {
        // BUG(goat): This check only behaves correctly on Windows - the mutex doesn't seem to disappear on Linux, .NET issue?
#if WIN32
        using var mutex = new Mutex(false, "XivLauncherIsPatching");

        if (!mutex.WaitOne(0, false))
        {
            App.ShowMessageBlocking(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."),
                "XIVLauncher");
            Environment.Exit(0);
            return false; // This line will not be run.
        }
#endif

        if (GameHelpers.CheckIsGameOpen())
        {
            App.ShowMessageBlocking(
                Loc.Localize("GameIsOpenError",
                    "The game and/or the official launcher are open. XIVLauncher cannot patch the game if this is the case.\nPlease close the official launcher and try again."),
                "XIVLauncher");

            return false;
        }

        using var installer = new PatchInstaller(App.Settings.KeepPatches ?? false);
        var patcher = new PatchManager(App.Settings.PatchAcquisitionMethod ?? AcquisitionMethod.Aria, App.Settings.PatchSpeedLimit, repository, pendingPatches, App.Settings.GamePath,
            App.Settings.PatchPath, installer, App.Launcher, sid);
        patcher.OnFail += PatcherOnFail;
        installer.OnFail += this.InstallerOnFail;

        /*
        Hide();

        PatchDownloadDialog progressDialog = _window.Dispatcher.Invoke(() =>
        {
            var d = new PatchDownloadDialog(patcher);
            if (_window.IsVisible)
                d.Owner = _window;
            d.Show();
            d.Activate();
            return d;
        });
        */

        this.App.StartLoading($"Now patching {repository.ToString().ToLowerInvariant()}...", canCancel: false, isIndeterminate: false);

        try
        {
            var token = new CancellationTokenSource();
            var statusThread = new Thread(UpdatePatchStatus);

            statusThread.Start();

            void UpdatePatchStatus()
            {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(30);

                    App.LoadingPage.Line2 = string.Format("Working on {0}/{1}", patcher.CurrentInstallIndex, patcher.Downloads.Count);
                    App.LoadingPage.Line3 = string.Format("{0} left to download at {1}/s", ApiHelpers.BytesToString(patcher.AllDownloadsLength < 0 ? 0 : patcher.AllDownloadsLength),
                        ApiHelpers.BytesToString(patcher.Speeds.Sum()));

                    App.LoadingPage.Progress = patcher.CurrentInstallIndex / (float)patcher.Downloads.Count;
                }
            }

            try
            {
                var aria2LogFile = new FileInfo(Path.Combine(App.Storage.GetFolder("logs").FullName, "launcher.log"));
                await patcher.PatchAsync(aria2LogFile, false).ConfigureAwait(false);
            }
            finally
            {
                token.Cancel();
                statusThread.Join(3000);
            }

            return true;
        }
        catch (PatchInstallerException ex)
        {
            var message = Loc.Localize("PatchManNoInstaller",
                "The patch installer could not start correctly.\n{0}\n\nIf you have denied access to it, please try again. If this issue persists, please contact us via Discord.");

            App.ShowMessageBlocking(string.Format(message, ex.Message), "XIVLauncher Error");
        }
        catch (NotEnoughSpaceException sex)
        {
            switch (sex.Kind)
            {
                case NotEnoughSpaceException.SpaceKind.Patches:
                    App.ShowMessageBlocking(
                        string.Format(
                            Loc.Localize("FreeSpaceError",
                                "There is not enough space on your drive to download patches.\n\nYou can change the location patches are downloaded to in the settings.\n\nRequired:{0}\nFree:{1}"),
                            ApiHelpers.BytesToString(sex.BytesRequired), ApiHelpers.BytesToString(sex.BytesFree)), "XIVLauncher Error");
                    break;

                case NotEnoughSpaceException.SpaceKind.AllPatches:
                    App.ShowMessageBlocking(
                        string.Format(
                            Loc.Localize("FreeSpaceErrorAll",
                                "There is not enough space on your drive to download all patches.\n\nYou can change the location patches are downloaded to in the XIVLauncher settings.\n\nRequired:{0}\nFree:{1}"),
                            ApiHelpers.BytesToString(sex.BytesRequired), ApiHelpers.BytesToString(sex.BytesFree)), "XIVLauncher Error");
                    break;

                case NotEnoughSpaceException.SpaceKind.Game:
                    App.ShowMessageBlocking(
                        string.Format(
                            Loc.Localize("FreeSpaceGameError",
                                "There is not enough space on your drive to install patches.\n\nYou can change the location the game is installed to in the settings.\n\nRequired:{0}\nFree:{1}"),
                            ApiHelpers.BytesToString(sex.BytesRequired), ApiHelpers.BytesToString(sex.BytesFree)), "XIVLauncher Error");
                    break;

                default:
                    Debug.Assert(false, "HandlePatchAsync:Invalid NotEnoughSpaceException.SpaceKind value.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during patching");
            App.ShowExceptionBlocking(ex, "HandlePatchAsync");
        }
        finally
        {
            App.State = LauncherApp.LauncherState.Main;
        }

        return false;
    }

    private void PatcherOnFail(PatchManager.FailReason reason, string versionId)
    {
        var dlFailureLoc = Loc.Localize("PatchManDlFailure",
            "XIVLauncher could not verify the downloaded game files. Please restart and try again.\n\nThis usually indicates a problem with your internet connection.\nIf this error persists, try using a VPN set to Japan.\n\nContext: {0}\n{1}");

        switch (reason)
        {
            case PatchManager.FailReason.DownloadProblem:
                App.ShowMessageBlocking(string.Format(dlFailureLoc, "Problem", versionId), "XIVLauncher Error");
                break;

            case PatchManager.FailReason.HashCheck:
                App.ShowMessageBlocking(string.Format(dlFailureLoc, "IsHashCheckPass", versionId), "XIVLauncher Error");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
        }

        Environment.Exit(0);
    }

    private void InstallerOnFail()
    {
        App.ShowMessageBlocking(
            Loc.Localize("PatchInstallerInstallFailed", "The patch installer ran into an error.\nPlease report this error.\n\nPlease try again or use the official launcher."),
            "XIVLauncher Error");

        Environment.Exit(0);
    }

    private async Task<bool> RepairGame(Launcher.LoginResult loginResult)
    {
        var doLogin = false;

        // BUG(goat): This check only behaves correctly on Windows - the mutex doesn't seem to disappear on Linux, .NET issue?
#if WIN32
        using var mutex = new Mutex(false, "XivLauncherIsPatching");

        if (!mutex.WaitOne(0, false))
        {
            App.ShowMessageBlocking(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."),
                "XIVLauncher");
            Environment.Exit(0);
            return false; // This line will not be run.
        }
#endif

        Debug.Assert(loginResult.PendingPatches != null, "loginResult.PendingPatches != null ASSERTION FAILED");
        Debug.Assert(loginResult.PendingPatches.Length != 0, "loginResult.PendingPatches.Length != 0 ASSERTION FAILED");

        Log.Information("STARTING REPAIR");

        // TODO: bundle the PatchInstaller with xl-core on Windows and run this remotely
        using var verify = new PatchVerifier(Program.CommonSettings, loginResult, 20, loginResult.OauthLogin.MaxExpansion, false);

        for (bool doVerify = true; doVerify;)
        {
            this.App.StartLoading($"Now repairing...", canCancel: false, isIndeterminate: false);

            verify.Start();

            var timer = new Timer(new TimerCallback((object? obj) =>
            {
                switch (verify.State)
                {
                    // TODO: show more progress info here
                    case PatchVerifier.VerifyState.DownloadMeta:
                        this.App.LoadingPage.Line2 = $"{verify.CurrentFile}";
                        this.App.LoadingPage.Line3 = $"{Math.Min(verify.PatchSetIndex + 1, verify.PatchSetCount)}/{verify.PatchSetCount} - {ApiHelpers.BytesToString(verify.Progress)}/{ApiHelpers.BytesToString(verify.Total)}";
                        this.App.LoadingPage.Progress = (float)(verify.Total != 0 ? (float)verify.Progress / (float)verify.Total : 0.0);
                        break;

                    case PatchVerifier.VerifyState.VerifyAndRepair:
                        this.App.LoadingPage.Line2 = $"{verify.CurrentFile}";
                        this.App.LoadingPage.Line3 = $"{Math.Min(verify.PatchSetIndex + 1, verify.PatchSetCount)}/{verify.PatchSetCount} - {Math.Min(verify.TaskIndex + 1, verify.TaskCount)}/{verify.TaskCount} - {ApiHelpers.BytesToString(verify.Progress)}/{ApiHelpers.BytesToString(verify.Total)}";
                        this.App.LoadingPage.Progress = (float)(verify.Total != 0 ? (float)verify.Progress / (float)verify.Total : 0);
                        break;

                    default:
                        this.App.LoadingPage.Line2 = "";
                        this.App.LoadingPage.Line3 = $"{Math.Min(verify.TaskIndex + 1, verify.TaskCount)}/{verify.TaskCount}";
                        this.App.LoadingPage.Progress = (float)(verify.State == PatchVerifier.VerifyState.Done ? 1.0 : 0);
                        break;
                }
            }
            ));
            timer.Change(0, 250);

            await verify.WaitForCompletion().ConfigureAwait(false);
            timer.Dispose();
            this.App.StopLoading();

            switch (verify.State)
            {
                case PatchVerifier.VerifyState.Done:
                    // TODO: ask the user if they want to login or rerun after repair
                    App.ShowMessageBlocking(verify.NumBrokenFiles switch
                        {
                            0 => Loc.Localize("GameRepairSuccess0", "All game files seem to be valid."),
                            1 => Loc.Localize("GameRepairSuccess1", "XIVLauncher has successfully repaired 1 game file."),
                            _ => string.Format(Loc.Localize("GameRepairSuccessPlural", "XIVLauncher has successfully repaired {0} game files."), verify.NumBrokenFiles),
                        });

                    doVerify = false;
                    break;

                case PatchVerifier.VerifyState.Error:
                    doLogin = false;

                    if (verify.LastException is NoVersionReferenceException)
                    {
                        App.ShowMessageBlocking(Loc.Localize("NoVersionReferenceError",
                                                        "The version of the game you are on cannot be repaired by XIVLauncher yet, as reference information is not yet available.\nPlease try again later."));
                    }
                    else
                    {
                        App.ShowMessageBlocking(verify.LastException + "\n\n" + Loc.Localize("GameRepairError", "An error occurred while repairing the game files.\nYou may have to reinstall the game."));
                    }

                    doVerify = false;
                    break;

                case PatchVerifier.VerifyState.Cancelled:
                    doLogin = doVerify = false;
                    break;
            }
        }


        return doLogin;
    }

    private void Hide()
    {
        Program.HideWindow();
    }

    private void Reactivate()
    {
        IsLoggingIn = false;
        this.App.State = LauncherApp.LauncherState.Main;

        Program.ShowWindow();
    }
}