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
        
        public bool Initialize(int appId)
        {
            throw new System.NotImplementedException();
        }

        public bool IsSteamRunning()
        {
            throw new System.NotImplementedException();
        }

        public bool Shutdown()
        {
            throw new System.NotImplementedException();
        }
    }
}