using System.IO;
using Newtonsoft.Json;

namespace XIVLauncher.Common.Dalamud
{
    public class DalamudSettings
    {
        public string? DalamudBetaKey { get; set; } = null;
        public bool DoDalamudRuntime { get; set; } = false;
        public string DalamudBetaKind { get; set; }
        public bool? OptOutMbCollection { get; set; }

        public static string ConfigPath => Path.Combine(Paths.RoamingPath, "dalamudConfig.json");

        public static DalamudSettings GetSettings()
        {
            var deserialized = File.Exists(ConfigPath) ? JsonConvert.DeserializeObject<DalamudSettings>(File.ReadAllText(ConfigPath)) : new DalamudSettings();
            deserialized ??= new DalamudSettings(); // In case the .json is corrupted
            return deserialized;
        }
    }
}