using System;
using System.Collections.Generic;
using System.Linq;

namespace XIVLauncher.Cache
{
    public class UniqueIdCache
    {
        private const int DAYS_TO_TIMEOUT = 2;

        private List<UniqueIdCacheEntry> _cache;

        public UniqueIdCache()
        {
            _cache = Settings.UniqueIdCache;
        }

        private void DeleteOldCaches()
        {
            _cache.RemoveAll(entry => (DateTime.Now - entry.CreationDate).TotalDays > DAYS_TO_TIMEOUT);
        }

        public bool HasValidCache(string userName)
        {
            return _cache.Any(entry => entry.UserName == userName && (DateTime.Now - entry.CreationDate).TotalDays <= DAYS_TO_TIMEOUT);
        }

        public (string Uid, int Region, int ExpansionLevel) GetCachedUid(string userName)
        {
            DeleteOldCaches();

            var cache = _cache.FirstOrDefault(entry => entry.UserName == userName && (DateTime.Now - entry.CreationDate).TotalDays <= DAYS_TO_TIMEOUT);

            if(cache == null)
                throw new Exception("Could not find a valid cache.");

            return (cache.UniqueId, cache.Region, cache.ExpansionLevel);
        }

        public void AddCachedUid(string userName, string uid, int region, int expansionLevel)
        {
             _cache.Add(new UniqueIdCacheEntry
             {
                 CreationDate = DateTime.Now,
                 UserName = userName,
                 UniqueId = uid,
                 Region = region,
                 ExpansionLevel = expansionLevel
             });

             Settings.UniqueIdCache = _cache;
             Settings.Save();
        }
    }
}
