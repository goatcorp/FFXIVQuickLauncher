using XIVLauncher.Core.Configuration.Linux;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<LinuxStartupType> startupTypeSetting;

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<LinuxStartupType>("Installation Type", "Choose how XIVLauncher will start and manage your game installation.",
                () => Program.Config.LinuxStartupType ?? LinuxStartupType.Managed, x => Program.Config.LinuxStartupType = x)
            {
                CheckValidity = type => type == LinuxStartupType.Managed ? "Managed wine installations are not yet implemented." : null,
            },

            new SettingsEntry<string>("Startup Command Line",
                "Set the command XIVLauncher will run to start applications via wine. Here, you should specify things like your wineprefix. %COMMAND% is aliased to the EXE file and its arguments by XIVLauncher.",
                () => Program.Config.LinuxStartCommandLine, s => Program.Config.LinuxStartCommandLine = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == LinuxStartupType.Command
            },
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsLinuxExclusive => true;

    public override string Title => "Linux";

    public override void Draw()
    {
        base.Draw();
    }
}