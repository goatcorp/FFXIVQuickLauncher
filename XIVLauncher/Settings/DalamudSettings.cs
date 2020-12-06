using System.Collections.Generic;
using System.IO;
using Dalamud;
using Newtonsoft.Json;

namespace XIVLauncher.Settings
{
    class DalamudSettings
    {
        public static bool OptOutMbUpload
        {
            get => DalamudConfig.OptOutMbCollection;
            set
            {
                var currentConfig = DalamudConfig;
                currentConfig.OptOutMbCollection = value;
                DalamudConfig = currentConfig;
            }
        }

        public static DalamudConfiguration DalamudConfig
        {
            get
            {
                var configPath = Path.Combine(Paths.RoamingPath, "dalamudConfig.json");

                if (File.Exists(configPath))
                    return JsonConvert.DeserializeObject<DalamudConfiguration>(File.ReadAllText(configPath));

                var newDalamudConfig = new DalamudConfiguration
                {
                    OptOutMbCollection = Properties.Settings.Default.OptOutMbUpload,
                    DutyFinderTaskbarFlash = true,
                    BadWords = new List<string>()
                };

                DalamudConfig = newDalamudConfig;
                return newDalamudConfig;
            }
            set => File.WriteAllText(Path.Combine(Paths.RoamingPath, "dalamudConfig.json"), JsonConvert.SerializeObject(value));
        }
    }
}
