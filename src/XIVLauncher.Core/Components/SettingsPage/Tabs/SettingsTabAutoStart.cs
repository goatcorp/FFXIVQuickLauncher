using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabAutoStart : SettingsTab
{
    public override SettingsEntry[] Entries { get; } = new SettingsEntry[] { };
    public override string Title => "Auto-Start";

    public override void Draw()
    {
        ImGui.Text("Please check back later.");

        base.Draw();
    }
}