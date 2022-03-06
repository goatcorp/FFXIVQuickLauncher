namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabGame : SettingsTab
{
    public override SettingsEntry[] Entries { get; } =
    {
        new SettingsEntry<DirectoryInfo>("Game Path", "Where the game is installed to.", () => Program.Config.GamePath, x => Program.Config.GamePath = x),
        new SettingsEntry<string>("Additional Arguments", "Additional args to start the game with", () => Program.Config.AdditionalArgs, x => Program.Config.AdditionalArgs = x),
    };

    public override string Title => "Game";

    public override void Draw()
    {
        base.Draw();
    }
}