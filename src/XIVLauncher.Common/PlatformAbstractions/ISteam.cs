using System;
using System.Threading.Tasks;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface ISteam
{
    void Initialize(uint appId);
    bool IsValid { get; }
    bool BLoggedOn { get; }
    bool BOverlayNeedsPresent { get; }
    void Shutdown();
    Task<byte[]?> GetAuthSessionTicketAsync();
    bool IsAppInstalled(uint appId);
    string GetAppInstallDir(uint appId);
    bool ShowGamepadTextInput(bool password, bool multiline, string description, int maxChars, string existingText = "");
    string GetEnteredGamepadText();
    bool ShowFloatingGamepadTextInput(EFloatingGamepadTextInputMode mode, int x, int y, int width, int height);
    bool IsRunningOnSteamDeck();
    uint GetServerRealTime();
    public void ActivateGameOverlayToWebPage(string url, bool modal = false);

    enum EFloatingGamepadTextInputMode
    {
        EnterDismisses,
        UserDismisses,
        Email,
        Numeric,
    }

    event Action<bool> OnGamepadTextInputDismissed;
}