using System.Collections;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabDebug : SettingsTab
{
    public override SettingsEntry[] Entries => Array.Empty<SettingsEntry>();
    public override string Title => "Debug Info";

    public override void Draw()
    {
        if (Program.IsSteamDeckHardware)
            ImGui.Text("Is Steam Deck hardware");

        if (Program.IsSteamDeckGamingMode)
            ImGui.Text("Is Steam Deck");

        ImGui.Separator();

        ImGui.PushTextWrapPos(0);

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            ImGui.TextUnformatted($"{entry.Key}={entry.Value}");
        }

        ImGui.PopTextWrapPos();

        base.Draw();
    }
}