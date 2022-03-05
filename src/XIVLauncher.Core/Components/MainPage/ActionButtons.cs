using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components.MainPage;

public class ActionButtons : Component
{
    public event Action? OnQueueButtonClicked;
    public event Action? OnStatusButtonClicked;
    public event Action? OnSettingsButtonClicked;

    public override void Draw()
    {
        var btnSize = new Vector2(80) * ImGuiHelpers.GlobalScale;

        ImGui.PushFont(FontManager.IconFont);

        if (ImGui.Button(FontAwesomeIcon.Clock.ToIconString(), btnSize))
        {
            this.OnQueueButtonClicked?.Invoke();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.Globe.ToIconString(), btnSize))
        {
            this.OnStatusButtonClicked?.Invoke();
        }

        ImGui.SameLine();

        if (ImGui.Button(FontAwesomeIcon.Cog.ToIconString(), btnSize))
        {
            this.OnSettingsButtonClicked?.Invoke();
        }

        ImGui.PopFont();

        base.Draw();
    }
}