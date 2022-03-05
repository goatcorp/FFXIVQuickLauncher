namespace XIVLauncher.Core.Components.MainPage;

public class MainPage : Page
{
    private readonly LoginFrame loginFrame;
    private readonly ActionButtons actionButtons;

    public MainPage(LauncherApp app)
        : base(app)
    {
        this.Children.Add(this.loginFrame = new LoginFrame());
        this.Children.Add(this.actionButtons = new ActionButtons());

        this.actionButtons.OnSettingsButtonClicked += () => this.App.State = LauncherApp.LauncherState.Settings;
    }
}