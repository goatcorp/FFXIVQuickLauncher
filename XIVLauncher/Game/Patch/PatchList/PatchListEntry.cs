using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Game.Patch.PatchList
{
    public class PatchListEntry
    {
        public string VersionId { get; set; }
        public string Url { get; set; }
        public string Hash { get; set; }
        public int Length { get; set; }
    }
}
