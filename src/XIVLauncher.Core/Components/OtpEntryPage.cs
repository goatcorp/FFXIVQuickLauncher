using System.Numerics;
using ImGuiNET;
using Serilog;

namespace XIVLauncher.Core.Components;

public class OtpEntryPage : Page
{
    private string otp = string.Empty;
    private bool appearing = false;

    public string? Result { get; private set; }

    public bool Cancelled { get; private set; }

    public OtpEntryPage(LauncherApp app)
        : base(app)
    {
        Program.Steam.OnGamepadTextInputDismissed += this.SteamOnOnGamepadTextInputDismissed;
    }

    private void SteamOnOnGamepadTextInputDismissed(bool success)
    {
        if (success)
        {
            Result = Program.Steam.GetEnteredGamepadText();
        }
    }

    public void Reset()
    {
        this.otp = string.Empty;
        this.appearing = true;
        this.Result = null;
        this.Cancelled = false;

        if (Program.Steam.IsValid && Program.Steam.IsRunningOnSteamDeck())
        {
            var success = Program.Steam.ShowGamepadTextInput(false, false, "Please enter your OTP", 6, string.Empty);
            Log.Verbose("ShowGamepadTextInput: {Success}", success);
        }
    }

    public override void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7f);

        var childSize = new Vector2(300, 200);
        var vpSize = ImGuiHelpers.ViewportSize;

        ImGui.SetNextWindowPos(new Vector2(vpSize.X / 2 - childSize.X / 2, vpSize.Y / 2 - childSize.Y / 2), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.4f);

        if (ImGui.BeginChild("###otp", childSize, true, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Dummy(new Vector2(40));

            // center text in window
            ImGuiHelpers.CenteredText("Please enter your OTP");

            const int INPUT_WIDTH = 150;
            ImGui.SetNextItemWidth(INPUT_WIDTH);
            ImGuiHelpers.CenterCursorFor(INPUT_WIDTH);

            if (this.appearing)
            {
                ImGui.SetKeyboardFocusHere(0);
                this.appearing = false;
            }

            var doEnter = ImGui.InputText("###otpInput", ref this.otp, 6, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue);

            var buttonSize = new Vector2(INPUT_WIDTH, 30);
            ImGuiHelpers.CenterCursorFor(INPUT_WIDTH);

            if (ImGui.Button("OK", buttonSize) || doEnter)
            {
                if (this.otp.Length == 6)
                {
                    this.Result = this.otp;
                }
            }
        }

        ImGui.EndChild();

        ImGui.PopStyleVar();

        base.Draw();
    }
}