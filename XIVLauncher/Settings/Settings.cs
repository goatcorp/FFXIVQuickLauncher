using Dalamud;
using Dalamud.Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using XIVLauncher.Addon;
using XIVLauncher.Cache;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;

namespace XIVLauncher.Settings
{
    public class LauncherSettings
    {
        #region Launcher Setting

        public DirectoryInfo GamePath { get; set; }
        public bool IsDx11 { get; set; }
        public bool AutologinEnabled { get; set; }
        public bool NeedsOtp { get; set; }
        public List<AddonEntry> AddonList { get; set; }
        public bool UniqueIdCacheEnabled { get; set; }
        public bool CharacterSyncEnabled { get; set; }
        public string AdditionalLaunchArgs { get; set; }
        public bool InGameAddonEnabled { get; set; }
        public bool SteamIntegrationEnabled { get; set; }
        public ClientLanguage Language { get; set; }

        #endregion

        #region SaveLoad

        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "launcherConfig.json");

        public void Save()
        {
            File.WriteAllText(ConfigPath,  JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
            }));
        }

        public static LauncherSettings Load()
        {
            if (!File.Exists(ConfigPath))
                return new LauncherSettings();

            var setting = JsonConvert.DeserializeObject<LauncherSettings>(File.ReadAllText(ConfigPath), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });

            setting.AddonList = EnsureDefaultAddon(setting.AddonList);

            return setting;
        }

        private static List<AddonEntry> EnsureDefaultAddon(List<AddonEntry> addonList)
        {
            if (!addonList.Any(entry => entry.Addon is RichPresenceAddon))
            {
                addonList.Add(new AddonEntry
                {
                    Addon = new RichPresenceAddon(),
                    IsEnabled = false
                });
            }

            return addonList;
        }

        #endregion

        #region Misc

        public void StartOfficialLauncher(bool isSteam)
        {
            Process.Start(Path.Combine(GamePath.FullName, "boot", "ffxivboot.exe"), isSteam ? "-issteam" : string.Empty);
        }

        #endregion
    }
}
