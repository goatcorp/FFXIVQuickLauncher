using System.Numerics;
using ImGuiNET;
using XIVLauncher.Core.Components.Common;

namespace XIVLauncher.Core.Components.LoadingPage;

public class LoadingPage : Page
{
    private const int SPINNER_RADIUS = 15;

    public bool IsIndeterminate { get; set; }
    public bool CanCancel { get; set; } = true;
    public bool CanDisableAutoLogin { get; set; }= false;
    public float Progress { get; set; }

    public string Line1 { get; set; } = "Loading text line 1";
    public string? Line2 { get; set; }
    public string? Line3 { get; set; }

    public string? ProgressText { get; set; } = null;

    private Spinner spinner;
    private Button cancelButton = new("Cancel");
    private Button disableAutoLoginButton = new("Disable auto login");

    private bool hasDisabledAutoLogin = false;

    public event Action? Cancelled;

    public LoadingPage(LauncherApp app)
        : base(app)
    {
        this.spinner = new Spinner(SPINNER_RADIUS, 5, ImGui.GetColorU32(ImGuiCol.ButtonActive));
        this.cancelButton.Click += () => this.Cancelled?.Invoke();

        this.disableAutoLoginButton.Click += () =>
        {
            this.hasDisabledAutoLogin = true;
            App.Settings.IsAutologin = false;
        };
    }

    public void Reset()
    {
        this.hasDisabledAutoLogin = false;
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

        var isDrawDisableAutoLogin = CanDisableAutoLogin && (App.Settings.IsAutologin ?? false);

        if (CanCancel || isDrawDisableAutoLogin)
        {
            ImGui.Dummy(new Vector2(20));
        }

        if (CanCancel)
        {
            this.cancelButton.Width = (int)vp.X / 4;
            ImGuiHelpers.CenterCursorFor(this.cancelButton.Width.Value);
            this.cancelButton.Draw();
        }

        if (isDrawDisableAutoLogin)
        {
            this.disableAutoLoginButton.Width = (int)vp.X / 4;
            ImGuiHelpers.CenterCursorFor(this.disableAutoLoginButton.Width.Value);
            this.disableAutoLoginButton.Draw();
        }
        else if (this.hasDisabledAutoLogin)
        {
            ImGuiHelpers.CenteredText("Auto login disabled on next start!");
        }

        ImGui.Dummy(new Vector2(20));

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

        Program.Invalidate(10);

        base.Draw();
    }
}