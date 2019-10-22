using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Game.Patch.PatchList
{
    class PatchListParser
    {
        public static PatchListEntry[] Parse(string list)
        {
            string[] lines = list.Split(
                new[] { "\r\n", "\r", "\n", Environment.NewLine },
                StringSplitOptions.None
            );
            
            var output = new List<PatchListEntry>();

            for (int i = 5; i < lines.Length - 2; i++)
            {
                var fields = lines[i].Split('	');
                
                output.Add(new PatchListEntry()
                {
                    Length = int.Parse(fields[0]),
                    VersionId = fields[4],
                    Hash = fields[7],
                    Url = fields[8]
                });
            }
            
            return output.ToArray();
        }
    }
}
