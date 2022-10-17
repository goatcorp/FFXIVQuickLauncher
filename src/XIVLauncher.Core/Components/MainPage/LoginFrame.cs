using System.Numerics;
using ImGuiNET;
using XIVLauncher.Core.Accounts.Secrets.Providers;
using XIVLauncher.Core.Components.Common;

namespace XIVLauncher.Core.Components.MainPage;

public class LoginFrame : Component
{
    private readonly MainPage mainPage;

    private readonly Input loginInput;
    private readonly Input passwordInput;
    private readonly Checkbox oneTimePasswordCheckbox;
    private readonly Checkbox useSteamServiceCheckbox;
    private readonly Checkbox autoLoginCheckbox;
    private readonly Button loginButton;

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
        get => this.useSteamServiceCheckbox.Value;
        set => this.useSteamServiceCheckbox.Value = value;
    }

    public bool IsAutoLogin
    {
        get => this.autoLoginCheckbox.Value;
        set => this.autoLoginCheckbox.Value = value;
    }

    public event Action<LoginAction>? OnLogin;

    private const string POPUP_ID_LOGINACTION = "popup_loginaction";

    public LoginFrame(MainPage mainPage)
    {
        this.mainPage = mainPage;

        void TriggerLogin()
        {
            this.OnLogin?.Invoke(LoginAction.Game);
        }

        this.loginInput = new Input("Username", "Enter your Username", new Vector2(12f, 0f), 128)
        {
            TakeKeyboardFocus = true
        };
        this.loginInput.Enter += TriggerLogin;

        this.passwordInput = new Input("Password", "Enter your password", new Vector2(12f, 0f), 128, flags: ImGuiInputTextFlags.Password | ImGuiInputTextFlags.NoUndoRedo);
        this.passwordInput.Enter += TriggerLogin;

        this.oneTimePasswordCheckbox = new Checkbox("Use one-time password");

        this.useSteamServiceCheckbox = new Checkbox("Use steam service");

        this.autoLoginCheckbox = new Checkbox("Log in automatically");

        this.loginButton = new Button("Login");
        this.loginButton.Click += TriggerLogin;
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

            this.oneTimePasswordCheckbox.Draw();
            this.useSteamServiceCheckbox.Draw();
            this.autoLoginCheckbox.Draw();

            ImGui.Dummy(new Vector2(10));

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

            if (Program.Secrets is DummySecretProvider)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
                ImGui.TextWrapped("Take care! No secrets provider is installed or configured. Passwords can't be saved.");
                ImGui.PopStyleColor();

                ImGui.Dummy(new Vector2(15));
            }

            ImGui.PushFont(FontManager.IconFont);

            var extraButtonSize = new Vector2(45) * ImGuiHelpers.GlobalScale;

            if (ImGui.Button(FontAwesomeIcon.CaretDown.ToIconString(), extraButtonSize))
            {
                ImGui.OpenPopup(POPUP_ID_LOGINACTION);
            }

            ImGui.SameLine();

            if (ImGui.Button(FontAwesomeIcon.UserFriends.ToIconString(), extraButtonSize))
            {
                this.mainPage.AccountSwitcher.Open();
            }

            ImGui.PopFont();
        }

        ImGui.EndChild();

        base.Draw();
    }
}