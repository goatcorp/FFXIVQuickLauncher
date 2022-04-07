using ImGuiNET;
using XIVLauncher.Core.Accounts;

namespace XIVLauncher.Core.Components.MainPage;

public class AccountSwitcher : Component
{
    private const string ACCOUNT_SWITCHER_POPUP_ID = "accountSwitcher";

    private readonly AccountManager manager;

    private bool doOpen = false;

    public event EventHandler<XivAccount>? AccountChanged;

    public AccountSwitcher(AccountManager manager)
    {
        this.manager = manager;
    }

    public void Open()
    {
        this.doOpen = true;
    }

    public override void Draw()
    {
        if (ImGui.BeginPopupContextItem(ACCOUNT_SWITCHER_POPUP_ID))
        {
            foreach (XivAccount account in this.manager.Accounts)
            {
                if (ImGui.Button(account.Id))
                {
                    this.AccountChanged?.Invoke(this, account);
                }
            }

            ImGui.EndPopup();
        }

        if (this.doOpen)
        {
            this.doOpen = false;
            ImGui.OpenPopup(ACCOUNT_SWITCHER_POPUP_ID);
        }

        base.Draw();
    }
}