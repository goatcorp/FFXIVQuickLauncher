using System.IO;
using Newtonsoft.Json;

namespace XIVLauncher.Settings
{
    class DalamudSettings
    {
        public bool DoDalamudTest { get; set; } = false;
        public bool DoDalamudRuntime { get; set; } = false;
        public string DalamudBetaKind { get; set; }
        public bool? OptOutMbCollection { get; set; }


        public static readonly string ConfigPath = Path.Combine(Paths.RoamingPath, "dalamudConfig.json");

        public static DalamudSettings GetSettings()
        {
            var deserialized = File.Exists(ConfigPath) ? JsonConvert.DeserializeObject<DalamudSettings>(File.ReadAllText(ConfigPath)) : new DalamudSettings();
            deserialized ??= new DalamudSettings(); // In case the .json is corrupted
            return deserialized;
        }
    }
}