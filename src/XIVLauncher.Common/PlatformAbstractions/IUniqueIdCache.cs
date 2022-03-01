namespace XIVLauncher.Common.PlatformAbstractions;

public interface IUniqueIdCache
{
    bool HasValidCache(string name);

    void Add(string name, string uid, int region, int maxExpansion);

    bool TryGet(string userName, out CachedUid cached);

    void Reset();

    public struct CachedUid
    {
        public string UniqueId;
        public int Region;
        public int MaxExpansion;
    }
}