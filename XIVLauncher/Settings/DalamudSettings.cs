using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud;
using Dalamud.Discord;
using Newtonsoft.Json;
using XIVLauncher.Dalamud;

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

        public static DiscordFeatureConfiguration DiscordFeatureConfig
        {
            get => DalamudConfig.DiscordFeatureConfig;
            set
            {
                var currentConfig = DalamudConfig;
                currentConfig.DiscordFeatureConfig = value;
                DalamudConfig = currentConfig;
            }
        }

        public static DalamudConfiguration DalamudConfig
        {
            get
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "dalamudConfig.json");

                if (File.Exists(configPath))
                    return JsonConvert.DeserializeObject<DalamudConfiguration>(File.ReadAllText(configPath));

                var discordFeatureConfig = JsonConvert.DeserializeObject<DiscordFeatureConfiguration>(Properties.Settings.Default
                                               .DiscordFeatureConfiguration) ?? new DiscordFeatureConfiguration
                                           {
                                               ChatTypeConfigurations = new List<ChatTypeConfiguration>(),
                                               ChatDelayMs = 1000
                                           };

                var newDalamudConfig = new DalamudConfiguration
                {
                    OptOutMbCollection = Properties.Settings.Default.OptOutMbUpload,
                    DiscordFeatureConfig = discordFeatureConfig,
                    BadWords = new List<string>(),
                    PluginConfigurations = new Dictionary<string, object>()
                };

                DalamudConfig = newDalamudConfig;
                return newDalamudConfig;
            }
            set => File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "dalamudConfig.json"), JsonConvert.SerializeObject(value));
        }
    }
}
