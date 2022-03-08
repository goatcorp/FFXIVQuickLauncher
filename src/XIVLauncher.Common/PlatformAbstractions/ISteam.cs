using System;

namespace XIVLauncher.Common.PlatformAbstractions;

public interface ISteam
{
    void Initialize(uint appId);
    bool IsValid { get; }
    void Shutdown();
    byte[] GetAuthSessionTicket();
    bool IsAppInstalled(uint appId);
    string GetAppInstallDir(uint appId);
    bool ShowGamepadTextInput(bool password, bool multiline, string description, int maxChars, string existingText = "");
    string GetEnteredGamepadText();
    bool IsRunningOnSteamDeck();
    uint GetServerRealTime();

    event Action<bool> OnGamepadTextInputDismissed;
}