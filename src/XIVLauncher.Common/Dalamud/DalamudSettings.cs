using System;
using System.IO;
using Newtonsoft.Json;
using Serilog;

namespace XIVLauncher.Common.Dalamud
{
    public class DalamudSettings
    {
        public string? DalamudBetaKey { get; set; } = null;
        public bool DoDalamudRuntime { get; set; } = false;
        public string DalamudBetaKind { get; set; }

        public static string GetConfigPath(DirectoryInfo configFolder) => Path.Combine(configFolder.FullName, "dalamudConfig.json");

        public static DalamudSettings GetSettings(DirectoryInfo configFolder)
        {
            var configPath = GetConfigPath(configFolder);
            DalamudSettings deserialized = null;

            try
            {
                deserialized = File.Exists(configPath) ? JsonConvert.DeserializeObject<DalamudSettings>(File.ReadAllText(configPath)) : new DalamudSettings();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Couldn't deserialize Dalamud settings");
            }

            deserialized ??= new DalamudSettings(); // In case the .json is corrupted
            return deserialized;
        }
    }
}