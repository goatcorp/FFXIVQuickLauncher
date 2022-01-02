using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace XIVLauncher.Dalamud
{
    class DalamudVersionInfo
    {
        public string AssemblyVersion { get; set; }
        public string SupportedGameVer { get; set; }
        public string RuntimeVersion { get; set; }
        public bool RuntimeRequired { get; set; }
        public string Key { get; set; }

        public static DalamudVersionInfo Load(FileInfo file) =>
            JsonConvert.DeserializeObject<DalamudVersionInfo>(File.ReadAllText(file.FullName));
    }
}