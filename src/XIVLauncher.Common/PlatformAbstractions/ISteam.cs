namespace XIVLauncher.Common.PlatformAbstractions;

public interface ISteam
{
    void Initialize(uint appId);
    bool IsValid { get; }
    void Shutdown();
    byte[] GetAuthSessionTicket();
    bool IsAppInstalled(uint appId);
    string GetAppInstallDir(uint appId);
}