using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.PlatformAbstractions
{
    public class CommonSteam : ISteam
    {
        private static CommonSteam _instance;
        
        public static CommonSteam Instance
        {
            get
            {
                _instance ??= new CommonSteam();
                return _instance;
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