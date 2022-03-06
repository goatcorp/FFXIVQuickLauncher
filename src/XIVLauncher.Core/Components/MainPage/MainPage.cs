using XIVLauncher.Common;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Windows;
using XIVLauncher.Core.Configuration;

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
            App.HandleContinationBlocking(t);
        });
    }

    public async Task Login(string username, string password, bool isOtp, bool isSteam, bool doingAutoLogin, LoginAction action)
    {
        var launcher = new Launcher(WindowsSteam.Instance, new UniqueIdCache(), Program.CommonSettings);
        var loginResult = await launcher.Login(username, password, string.Empty, isSteam, true, Program.Config.GamePath, false).ConfigureAwait(true);

        if (loginResult.State != Launcher.LoginState.Ok)
        {
            throw new Exception($"poop: {loginResult.State}");
        }
    }

    private void Reactivate()
    {
        IsLoggingIn = false;
    }
}