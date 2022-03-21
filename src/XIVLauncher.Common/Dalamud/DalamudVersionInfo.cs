using System.IO;
using Newtonsoft.Json;

namespace XIVLauncher.Common.Dalamud
{
    internal class DalamudVersionInfo
    {
        public string AssemblyVersion { get; set; }
        public string SupportedGameVer { get; set; }
        public string RuntimeVersion { get; set; }
        public bool RuntimeRequired { get; set; }
        public string Key { get; set; }
        public string DownloadUrl { get; set; }

        public static DalamudVersionInfo Load(FileInfo file) =>
            JsonConvert.DeserializeObject<DalamudVersionInfo>(File.ReadAllText(file.FullName));
    }
}