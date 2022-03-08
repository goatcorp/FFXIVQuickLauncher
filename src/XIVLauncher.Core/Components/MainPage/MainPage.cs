using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Windows;

namespace XIVLauncher.Core.Components.MainPage;

public class MainPage : Page
{
    private readonly LoginFrame loginFrame;
    private readonly ActionButtons actionButtons;

    public bool IsLoggingIn { get; private set; }

    public MainPage(LauncherApp app)
        : base(app)
    {
        this.Children.Add(this.loginFrame = new LoginFrame(this));
        this.Children.Add(this.actionButtons = new ActionButtons());

        this.loginFrame.OnLogin += this.ProcessLogin;
        this.actionButtons.OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings;
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
                App.OpenModalBlocking("The game and/or the official launcher are open. XIVLauncher cannot repair the game if this is the case.\nPlease close them and try again.", "XIVLauncher");

                Reactivate();
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
        var otp = string.Empty;

        if (isOtp)
        {
            App.AskForOtp();
            otp = App.WaitForOtp();
        }

        if (otp == null)
            return;

        var loginResult = await Program.Launcher.Login(username, password, otp, isSteam, true, Program.Config.GamePath, false).ConfigureAwait(true);

        if (loginResult.State != Launcher.LoginState.Ok)
        {
            throw new Exception($"poop: {loginResult.State}");
        }

        IGameRunner runner;

        if (OperatingSystem.IsWindows())
        {
            runner = new WindowsGameRunner(null, false, Program.Config.DalamudLoadMethod ?? DalamudLoadMethod.DllInject);
        }
        else
        {
            throw new NotImplementedException();
        }

        Program.Launcher.LaunchGame(runner, loginResult.UniqueId, loginResult.OauthLogin.Region, loginResult.OauthLogin.MaxExpansion, this.loginFrame.IsSteam, Program.Config.AdditionalArgs,
            Program.Config.GamePath,
            true, Program.Config.ClientLanguage ?? ClientLanguage.English, true, Program.Config.DpiAwareness ?? DpiAwareness.Unaware);
    }

    private void Reactivate()
    {
        IsLoggingIn = false;
    }
}