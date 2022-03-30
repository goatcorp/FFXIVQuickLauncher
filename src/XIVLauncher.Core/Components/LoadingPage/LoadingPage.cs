using System.Numerics;
using ImGuiNET;
using XIVLauncher.Core.Components.Common;

namespace XIVLauncher.Core.Components.LoadingPage;

public class LoadingPage : Page
{
    private const int SPINNER_RADIUS = 15;

    public bool IsIndeterminate { get; set; }
    public bool CanCancel { get; set; } = true;
    public float Progress { get; set; }

    public string Line1 { get; set; } = "Loading text line 1";
    public string? Line2 { get; set; }
    public string? Line3 { get; set; }

    public string? ProgressText { get; set; } = null;

    private Spinner spinner;
    private Button cancelButton = new("Cancel");

    public event Action? Cancelled;

    public LoadingPage(LauncherApp app)
        : base(app)
    {
        this.spinner = new Spinner(SPINNER_RADIUS, 5, ImGui.GetColorU32(ImGuiCol.ButtonActive));
        this.cancelButton.Click += () => this.Cancelled?.Invoke();
    }

    public override void Draw()
    {
        var vp = ImGuiHelpers.ViewportSize;


        ImGui.SetCursorPosY(vp.Y / 2 - 100);

        // center text in window
        ImGuiHelpers.CenteredText(Line1);

        if (!string.IsNullOrEmpty(Line2))
        {
            ImGui.Dummy(new Vector2(2));
            ImGuiHelpers.CenteredText(Line2);
        }

        if (!string.IsNullOrEmpty(Line3))
        {
            ImGui.Dummy(new Vector2(2));
            ImGuiHelpers.CenteredText(Line3);
        }

        if (IsIndeterminate)
        {
            ImGuiHelpers.CenterCursorFor(SPINNER_RADIUS * 2);
            this.spinner.Draw();
        }
        else
        {
            var width = vp.X / 3;
            ImGuiHelpers.CenterCursorFor((int)width);
            ImGui.ProgressBar(Progress, new Vector2(width, 20), ProgressText);
        }

        if (CanCancel)
        {
            this.cancelButton.Width = (int)vp.X / 4;
            ImGuiHelpers.CenterCursorFor(this.cancelButton.Width.Value);
            this.cancelButton.Draw();
        }

        base.Draw();
    }
}