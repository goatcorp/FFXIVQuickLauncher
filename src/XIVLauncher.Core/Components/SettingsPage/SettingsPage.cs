using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.SettingsPage;

public class SettingsPage : Page
{
    private readonly SettingsTab[] tabs =
    {
        new SettingsTabGame(),
        new SettingsTabWine(),
        new SettingsTabAbout(),
    };

    public SettingsPage(LauncherApp app)
        : base(app)
    {
    }

    public override void OnShow()
    {
        foreach (var settingsTab in this.tabs)
        {
            settingsTab.Load();
        }

        base.OnShow();
    }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("###settingsTabs"))
        {
            foreach (var settingsTab in this.tabs)
            {
                if (settingsTab.IsLinux && !OperatingSystem.IsLinux())
                    continue;

                if (ImGui.BeginTabItem(settingsTab.Title))
                {
                    settingsTab.Draw();
                    ImGui.EndTabItem();
                }
            }
        }

        ImGui.SetCursorPos(ImGuiHelpers.ViewportSize - new Vector2(60));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 100f);
        ImGui.PushFont(FontManager.IconFont);

        if (ImGui.Button(FontAwesomeIcon.Check.ToIconString(), new Vector2(40)))
        {
            this.App.State = LauncherApp.LauncherState.Main;
        }

        ImGui.PopStyleVar();
        ImGui.PopFont();

        base.Draw();
    }
}