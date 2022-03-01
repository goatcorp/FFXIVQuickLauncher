using Steamworks;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Windows
{
    public class WindowsSteam : ISteam
    {
        private static WindowsSteam instance;

        public static WindowsSteam Instance
        {
            get
            {
                instance ??= new WindowsSteam();
                return instance;
            }
        }

        public void Initialize(uint appId)
        {
            SteamClient.Init(appId);
        }

        public bool IsValid => SteamClient.IsValid && SteamClient.IsLoggedOn;

        public void Shutdown()
        {
            SteamClient.Shutdown();
        }

        public byte[] GetAuthSessionTicket()
        {
            return SteamUser.GetAuthSessionTicketAsync().GetAwaiter().GetResult().Data;
        }

        public bool IsAppInstalled(uint appId)
        {
            return SteamApps.IsAppInstalled(appId);
        }

        public string GetAppInstallDir(uint appId)
        {
            return SteamApps.AppInstallDir(appId);
        }
    }
}