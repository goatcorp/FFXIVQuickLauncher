namespace XIVLauncher.Common.PlatformAbstractions;

public interface ISteam
{
    bool Initialize(int appId);
    bool IsSteamRunning();
    bool Shutdown();
}