using System.IO;
using Newtonsoft.Json;

namespace XIVLauncher.Settings
{
    class DalamudSettings
    {
        public class DalamudConfiguration
        {
            public bool DoDalamudTest { get; set; } = false;
        }

        public DalamudConfiguration DalamudConfig { get; set; }

        public DalamudSettings(string configPath)
        {
            if (File.Exists(configPath))
                DalamudConfig = JsonConvert.DeserializeObject<DalamudConfiguration>(File.ReadAllText(configPath));
            else
                DalamudConfig = new DalamudConfiguration();
        }
    }
}
