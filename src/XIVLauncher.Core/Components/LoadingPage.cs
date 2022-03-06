using ImGuiNET;
using XIVLauncher.Core.Components.Loading;

namespace XIVLauncher.Core.Components;

public class LoadingPage : Page
{
    public bool IsIndeterminate { get; set; }

    public string Line1 { get; set; }
    public string Line2 { get; set; }
    public string Line3 { get; set; }

    public LoadingPage(LauncherApp app)
        : base(app)
    {
        this.Children.Add(new Spinner(15, 5, ImGui.GetColorU32(ImGuiCol.ButtonActive)));
    }

    public override void Draw()
    {
        ImGui.Text(Line1);
        ImGui.Text(Line2);
        ImGui.Text(Line3);

        base.Draw();
    }
}