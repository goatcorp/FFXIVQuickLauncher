using System;

namespace XIVLauncher.Cache
{
    internal class UniqueIdCacheEntry
    {
        public string UserName { get; set; }
        public string UniqueId { get; set; }
        public int Region { get; set; }
        public int ExpansionLevel { get; set; }

        public DateTime TimeoutDate { get; set; }
    }
}