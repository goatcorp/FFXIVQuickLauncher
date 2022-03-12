using XIVLauncher.Common;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabGame : SettingsTab
{
    public override SettingsEntry[] Entries { get; } =
    {
        new SettingsEntry<DirectoryInfo>("Game Path", "Where the game is installed to.", () => Program.Config.GamePath, x => Program.Config.GamePath = x)
        {
            CheckValidity = x =>
            {
                if (x == null)
                    return "Game path is not set.";

                if (x.Name == "game" || x.Name == "boot")
                    return "Please select the path containing the folders \"game\" and \"boot\", not the folders itself.";

                return null;
            }
        },
        new SettingsEntry<string>("Additional Arguments", "Additional args to start the game with", () => Program.Config.AdditionalArgs, x => Program.Config.AdditionalArgs = x),
        new SettingsEntry<ClientLanguage>("Game Language", "Select the game's language.", () => Program.Config.ClientLanguage ?? ClientLanguage.English, x => Program.Config.ClientLanguage = x),
        new SettingsEntry<DpiAwareness>("Game DPI Awareness", "Select the game's DPI Awareness. Change this if the game's scaling looks wrong.", () => Program.Config.DpiAwareness ?? DpiAwareness.Unaware, x => Program.Config.DpiAwareness = x),
    };

    public override string Title => "Game";

    public override void Draw()
    {
        base.Draw();
    }
}