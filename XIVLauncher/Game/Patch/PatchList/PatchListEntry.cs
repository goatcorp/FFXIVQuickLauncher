using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XIVLauncher.Game.Patch.PatchList
{
    public class PatchListEntry
    {
        private static Regex urlRegex = new Regex(".*/((game|boot)/([a-zA-Z0-9]+)/.*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string VersionId { get; set; }
        public string HashType { get; set; }
        public string Url { get; set; }
        public long HashBlockSize { get; set; }
        public string[] Hashes { get; set; }
        public long Length { get; set; }

        public override string ToString() => $"{this.GetRepoName()}/{VersionId}";

        private Match Deconstruct() => urlRegex.Match(this.Url);

        public string GetRepoName() => this.Deconstruct().Groups[3].Captures[0].Value;

        public string GetFilePath() => this.Deconstruct().Groups[1].Captures[0].Value.Replace('/', Path.DirectorySeparatorChar);
    }
}
