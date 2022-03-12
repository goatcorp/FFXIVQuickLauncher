using System;
using System.Threading.Tasks;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface ISteam
{
    void Initialize(uint appId);
    bool IsValid { get; }
    bool BLoggedOn();
    void Shutdown();
    Task<byte[]?> GetAuthSessionTicketAsync();
    bool IsAppInstalled(uint appId);
    string GetAppInstallDir(uint appId);
    bool ShowGamepadTextInput(bool password, bool multiline, string description, int maxChars, string existingText = "");
    string GetEnteredGamepadText();
    bool IsRunningOnSteamDeck();
    uint GetServerRealTime();

    event Action<bool> OnGamepadTextInputDismissed;
}