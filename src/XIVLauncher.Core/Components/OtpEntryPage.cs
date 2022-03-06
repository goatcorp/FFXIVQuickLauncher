using ImGuiNET;

namespace XIVLauncher.Core.Components;

public class OtpEntryPage : Page
{
    private string otp = string.Empty;

    public string? Result { get; private set; }

    public OtpEntryPage(LauncherApp app)
        : base(app)
    {
    }

    public void Reset()
    {
        this.otp = string.Empty;
        this.Result = null;
    }

    public override void Draw()
    {
        var doEnter = ImGui.InputText("###otpInput", ref this.otp, 6, ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.EnterReturnsTrue);

        if (ImGui.Button("OK") || doEnter)
        {
            if (this.otp.Length == 6)
            {
                this.Result = this.otp;
            }
        }

        base.Draw();
    }
}