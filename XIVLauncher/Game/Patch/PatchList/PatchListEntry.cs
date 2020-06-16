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
        public long HashBlockSize { get; set; }
        public string[] Hashes { get; set; }
        public long Length { get; set; }

        public override string ToString() => VersionId;
    }
}
