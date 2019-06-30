using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Cache
{
    class UniqueIdCacheEntry
    {
        public string UserName { get; set; }
        public string UniqueId { get; set; }
        public int Region { get; set; }
        public int ExpansionLevel { get; set; }

        public DateTime TimeoutDate { get; set; }
    }
}
