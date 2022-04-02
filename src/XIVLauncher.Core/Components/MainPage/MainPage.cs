using System.Diagnostics;
using ImGuiNET;
using System.Numerics;
using CheapLoc;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Windows;
using XIVLauncher.Core.Accounts;
using XIVLauncher.Core.Runners;

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
        this.newsFrame = new NewsFrame(this);

        this.actionButtons = new ActionButtons();

        this.loginFrame.OnLogin += this.ProcessLogin;
        this.actionButtons.OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings;

        this.Padding = new Vector2(32f, 32f);
    }

    public override void Draw()
    {
        base.Draw();

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(32f, 32f));
        this.newsFrame.Draw();

        ImGui.SameLine();

        this.loginFrame.Draw();

        this.actionButtons.Draw();
    }

    private void ProcessLogin(LoginAction action)
    {
        if (this.IsLoggingIn)
            return;

        this.App.StartLoading("Logging in...");

        Task.Run(async () =>
        {
            if (Util.CheckIsGameOpen() && action == LoginAction.Repair)
            {
                App.ShowMessageBlocking("The game and/or the official launcher are open. XIVLauncher cannot repair the game if this is the case.\nPlease close them and try again.", "XIVLauncher");

                Reactivate();
                return;
            }

            if (Repository.Ffxiv.GetVer(App.Settings.GamePath) == Constants.BASE_GAME_VERSION &&
                App.Settings.UidCacheEnabled == true)
            {
                App.ShowMessageBlocking(
                    "You enabled the UID cache in the patcher settings.\nThis setting does not allow you to reinstall FFXIV.\n\nIf you want to reinstall FFXIV, please take care to disable it first.",
                    "XIVLauncher Error");

                this.Reactivate();
                return;
            }

            IsLoggingIn = true;

            await this.Login(this.loginFrame.Username, this.loginFrame.Password, this.loginFrame.IsOtp, this.loginFrame.IsSteam, false, action);
        }).ContinueWith(t =>
        {
            if (!App.HandleContinationBlocking(t))
                this.Reactivate();
        });
    }

    public async Task Login(string username, string password, bool isOtp, bool isSteam, bool doingAutoLogin, LoginAction action)
    {
        if (action == LoginAction.Fake)
        {
            App.Launcher.LaunchGame(new WindowsGameRunner(null, false, DalamudLoadMethod.DllInject), "0", 1, 2, false, "", App.Settings.GamePath, true, ClientLanguage.Japanese, true,
                DpiAwareness.Unaware);
            return;
        }

        var bootRes = await HandleBootCheck().ConfigureAwait(false);

        if (!bootRes)
            return;

        var otp = string.Empty;

        if (isOtp)
        {
            App.AskForOtp();
            otp = App.WaitForOtp();
        }

        if (otp == null)
            return;

        PersistAccount(username, password, isOtp, isSteam);

        var loginResult = await App.Launcher.Login(username, password, otp, isSteam, true, App.Settings.GamePath, false, false).ConfigureAwait(true);

        if (loginResult.State != Launcher.LoginState.Ok)
        {
            throw new Exception($"Unexpected LoginState: {loginResult.State}");
        }

        IGameRunner runner;

        if (OperatingSystem.IsWindows())
        {
            runner = new WindowsGameRunner(null, false, App.Settings.DalamudLoadMethod ?? DalamudLoadMethod.DllInject);
        }
        else if (OperatingSystem.IsLinux())
        {
            runner = new LinuxGameRunner();
        }
        else
        {
            throw new NotImplementedException();
        }

        App.Launcher.LaunchGame(runner, loginResult.UniqueId, loginResult.OauthLogin.Region, loginResult.OauthLogin.MaxExpansion, this.loginFrame.IsSteam, App.Settings.AdditionalArgs,
            App.Settings.GamePath,
            true, App.Settings.ClientLanguage ?? ClientLanguage.English, true, App.Settings.DpiAwareness ?? DpiAwareness.Unaware);
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
                Log.Error(ex, "Unable to check boot version.");
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

    private async Task<bool> TryHandlePatchAsync(Repository repository, PatchListEntry[] pendingPatches, string sid)
    {
        using var mutex = new Mutex(false, "XivLauncherIsPatching");

        if (!mutex.WaitOne(0, false))
        {
            App.ShowMessageBlocking(Loc.Localize("PatcherAlreadyInProgress", "XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open."),
                "XIVLauncher");
            Environment.Exit(0);
            return false; // This line will not be run.
        }

        if (Util.CheckIsGameOpen())
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

        try
        {
            await patcher.PatchAsync().ConfigureAwait(false);
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
                            Util.BytesToString(sex.BytesRequired), Util.BytesToString(sex.BytesFree)), "XIVLauncher Error");
                    break;

                case NotEnoughSpaceException.SpaceKind.AllPatches:
                    App.ShowMessageBlocking(
                        string.Format(
                            Loc.Localize("FreeSpaceErrorAll",
                                "There is not enough space on your drive to download all patches.\n\nYou can change the location patches are downloaded to in the XIVLauncher settings.\n\nRequired:{0}\nFree:{1}"),
                            Util.BytesToString(sex.BytesRequired), Util.BytesToString(sex.BytesFree)), "XIVLauncher Error");
                    break;

                case NotEnoughSpaceException.SpaceKind.Game:
                    App.ShowMessageBlocking(
                        string.Format(
                            Loc.Localize("FreeSpaceGameError",
                                "There is not enough space on your drive to install patches.\n\nYou can change the location the game is installed to in the settings.\n\nRequired:{0}\nFree:{1}"),
                            Util.BytesToString(sex.BytesRequired), Util.BytesToString(sex.BytesFree)), "XIVLauncher Error");
                    break;

                default:
                    Debug.Assert(false, "HandlePatchAsync:Invalid NotEnoughSpaceException.SpaceKind value.");
                    break;
            }
        }
        catch (Exception ex)
        {
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

    private void Reactivate()
    {
        IsLoggingIn = false;
        this.App.State = LauncherApp.LauncherState.Main;
    }
}