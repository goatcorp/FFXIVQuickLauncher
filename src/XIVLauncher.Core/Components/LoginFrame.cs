using System.Numerics;
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

    private const string POPUP_ID_LOGINACTION = "popup_loginaction";

    public override void Draw()
    {
        if (ImGui.BeginPopupContextItem(POPUP_ID_LOGINACTION))
        {
            if (ImGui.MenuItem("Launch without Dalamud"))
            {
                this.OnLogin?.Invoke(LoginAction.GameNoDalamud);
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Patch without launching"))
            {
                this.OnLogin?.Invoke(LoginAction.GameNoLaunch);
            }

            ImGui.Separator();

            if (ImGui.MenuItem("Repair game files"))
            {
                this.OnLogin?.Invoke(LoginAction.Repair);
            }

            if (App.IsDebug)
            {
                ImGui.Separator();

                if (ImGui.MenuItem("Fake Login"))
                {
                    this.OnLogin?.Invoke(LoginAction.Fake);
                }
            }

            ImGui.EndPopup();
        }

        ImGui.InputText("SE ID", ref loginUsername, 128);
        ImGui.InputText("Password", ref loginPassword, 128, ImGuiInputTextFlags.Password | ImGuiInputTextFlags.NoUndoRedo);

        if (ImGui.Button("Login", new Vector2(100, 30) * ImGuiHelpers.GlobalScale))
        {
            OnLogin?.Invoke(LoginAction.Game);
        }

        ImGui.OpenPopupOnItemClick(POPUP_ID_LOGINACTION, ImGuiPopupFlags.MouseButtonRight);

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1f, 1f));
        ImGui.SameLine();

        ImGui.PushFont(FontManager.IconFont);

        if (ImGui.Button(FontAwesomeIcon.CaretDown.ToIconString(), new Vector2(20, 30) * ImGuiHelpers.GlobalScale)) // TODO: "Down arrow" icon
        {
            ImGui.OpenPopup(POPUP_ID_LOGINACTION);
        }

        ImGui.PopFont();

        ImGui.PopStyleVar();

        base.Draw();
    }
}