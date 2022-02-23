using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.PlatformAbstractions
{
    public class CommonUniqueIdCache : IUniqueIdCache
    {
        private static CommonUniqueIdCache _instance;
        
        public static CommonUniqueIdCache Instance
        {
            get
            {
                _instance ??= new CommonUniqueIdCache();
                return _instance;
            }
        }
        
        public bool HasValidCache(string name)
        {
            throw new System.NotImplementedException();
        }

        public void Add(string name, string uid, int region, int maxExpansion)
        {
            throw new System.NotImplementedException();
        }

        public bool TryGet(string name, out IUniqueIdCache.CachedUid cached)
        {
            throw new System.NotImplementedException();
        }
    }
}