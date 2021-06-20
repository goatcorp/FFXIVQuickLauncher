using System.IO;
using Newtonsoft.Json;

namespace XIVLauncher.Settings
{
    static class DalamudSettings
    {
        public class DalamudConfiguration
        {
            public bool DoDalamudTest { get; set; } = false;
            public bool DoDalamudRuntime { get; set; } = false;
            public string DalamudBetaKind { get; set; }
            public bool? OptOutMbCollection { get; set; }
        }

        public static string configPath = Path.Combine(Paths.RoamingPath, "dalamudConfig.json");

        public static DalamudConfiguration GetSettings()
        {
            if (File.Exists(configPath))
                return JsonConvert.DeserializeObject<DalamudConfiguration>(File.ReadAllText(configPath));
            else
                return new DalamudConfiguration();
        }
    }
}
