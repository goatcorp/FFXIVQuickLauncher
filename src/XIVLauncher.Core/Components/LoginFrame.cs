using ImGuiNET;

namespace XIVLauncher.Core.Components;

public class LoginFrame : Component
{
    private string loginUsername = string.Empty;
    private string loginPassword = string.Empty;

    public string Username => this.loginUsername;
    public string Password => this.loginPassword;

    public event Action<LoginAction>? OnLogin;

    public enum LoginAction
    {
        Game,
        GameNoDalamud,
        GameNoLaunch,
        Repair,
        Fake,
    }

    public override void Draw()
    {
        ImGui.InputText("SE ID", ref loginUsername, 128);
        ImGui.InputText("Password", ref loginPassword, 128, ImGuiInputTextFlags.Password);

        if (ImGui.Button("Login"))
        {
            OnLogin?.Invoke(LoginAction.Game);
        }

        base.Draw();
    }
}