using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Core.Configuration;

public class UniqueIdCache : IUniqueIdCache
{
    public bool HasValidCache(string name)
    {
        return false;
    }

    public void Add(string name, string uid, int region, int maxExpansion)
    {

    }

    public bool TryGet(string userName, out IUniqueIdCache.CachedUid cached)
    {
        cached = default;
        return false;
    }

    public void Reset()
    {

    }
}