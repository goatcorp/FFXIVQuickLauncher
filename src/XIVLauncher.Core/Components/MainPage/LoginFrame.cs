using System.Numerics;
using ImGuiNET;
using XIVLauncher.Core.Components.Common;

namespace XIVLauncher.Core.Components.MainPage;

public class LoginFrame : Component
{
    private readonly MainPage mainPage;

    private readonly Input loginInput;
    private readonly Input passwordInput;
    private readonly Input oneTimePasswordInput;
    private readonly Checkbox oneTimePasswordCheckbox;
    private readonly Checkbox useSteamServiceCheckbox;
    private readonly Button loginButton;

    private bool isOtp = false;
    private bool isSteam = false;

    public string Username
    {
        get => this.loginInput.Value;
        set => this.loginInput.Value = value;
    }

    public string Password
    {
        get => this.passwordInput.Value;
        set => this.passwordInput.Value = value;
    }

    public bool IsOtp
    {
        get => this.oneTimePasswordCheckbox.Value;
        set => this.oneTimePasswordCheckbox.Value = value;
    }

    public bool IsSteam
    {
        get => this.isSteam;
        set => this.isSteam = value;
    }

    public event Action<LoginAction>? OnLogin;

    private const string POPUP_ID_LOGINACTION = "popup_loginaction";

    public LoginFrame(MainPage mainPage)
    {
        this.mainPage = mainPage;

        this.loginInput = new Input("Square Enix ID", "Enter your Square Enix ID", new Vector2(12f, 0f), 128);
        this.passwordInput = new Input("Password", "Enter your password", new Vector2(12f, 0f), 128, flags: ImGuiInputTextFlags.Password | ImGuiInputTextFlags.NoUndoRedo);
        this.oneTimePasswordInput = new Input("One-time password", "Enter your one-time password", new Vector2(12f, 0f), 6, false, ImGuiInputTextFlags.CharsDecimal);

        this.oneTimePasswordCheckbox = new Checkbox("Use one-time password");
        this.oneTimePasswordCheckbox.OnChange += newValue => { this.oneTimePasswordInput.IsEnabled = newValue; };

        this.useSteamServiceCheckbox = new Checkbox("Use steam service");
        this.useSteamServiceCheckbox.OnChange += newValue => { this.isSteam = newValue; };

        this.loginButton = new Button("Login");
        this.loginButton.Click += () => { this.OnLogin?.Invoke(LoginAction.Game); };
    }

    private Vector2 GetSize()
    {
        var vp = ImGuiHelpers.ViewportSize;
        return new Vector2(-1, vp.Y - 128f);
    }

    public override void Draw()
    {
        if (ImGui.BeginChild("###loginFrame", this.GetSize()))
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(32f, 32f));
            this.loginInput.Draw();
            this.passwordInput.Draw();
            this.oneTimePasswordInput.Draw();

            this.oneTimePasswordCheckbox.Draw();

            this.loginButton.Draw();

            ImGui.PopStyleVar();

            ImGui.NewLine();

            ImGui.OpenPopupOnItemClick(POPUP_ID_LOGINACTION, ImGuiPopupFlags.MouseButtonRight);

            ImGui.PushStyleColor(ImGuiCol.PopupBg, ImGuiColors.BlueShade1);

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

                if (LauncherApp.IsDebug)
                {
                    ImGui.Separator();

                    if (ImGui.MenuItem("Fake Login"))
                    {
                        this.OnLogin?.Invoke(LoginAction.Fake);
                    }
                }

                ImGui.EndPopup();
            }

            ImGui.PopStyleColor();

            ImGui.PushFont(FontManager.IconFont);

            if (ImGui.Button(FontAwesomeIcon.CaretDown.ToIconString(), new Vector2(30, 30) * ImGuiHelpers.GlobalScale))
            {
                ImGui.OpenPopup(POPUP_ID_LOGINACTION);
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.UserFriends.ToIconString(), new Vector2(30, 30) * ImGuiHelpers.GlobalScale))
            {

            }

            ImGui.PopFont();
        }

        ImGui.EndChild();

        base.Draw();
    }
}