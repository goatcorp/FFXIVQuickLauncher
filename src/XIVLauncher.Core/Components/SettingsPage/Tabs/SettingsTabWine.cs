namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    public override SettingsEntry[] Entries { get; } =
    {
    };

    public override bool IsLinux => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();
    }
}