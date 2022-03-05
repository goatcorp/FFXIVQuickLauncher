namespace XIVLauncher.Core.Components;

public class Page : Component
{
    protected LauncherApp App { get; }

    public Page(LauncherApp app)
    {
        this.App = app;
    }
}