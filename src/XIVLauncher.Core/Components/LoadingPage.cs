using System.Numerics;
using ImGuiNET;
using XIVLauncher.Core.Components.Loading;

namespace XIVLauncher.Core.Components;

public class LoadingPage : Page
{
    public bool IsIndeterminate { get; set; }

    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string Line3 { get; set; }

    private Spinner spinner;

    public LoadingPage(LauncherApp app)
        : base(app)
    {
        this.spinner = new Spinner(15, 5, ImGui.GetColorU32(ImGuiCol.ButtonActive));
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7f);

        var childSize = new Vector2(300, 200);
        var vpSize = ImGuiHelpers.ViewportSize;

        ImGui.SetNextWindowPos(new Vector2(vpSize.X / 2 - childSize.X / 2, vpSize.Y / 2 - childSize.Y / 2), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.4f);

        if (ImGui.BeginChild("###loading", childSize, true, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Dummy(new Vector2(40));

            // center text in window
            ImGuiHelpers.CenteredText(Line1);

            this.spinner.Draw();
        }

        ImGui.EndChild();

        ImGui.PopStyleVar();

        base.Draw();
    }
}