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

namespace XIVLauncher
{
    class Settings
    {
        public Action LanguageChanged;

        #region Launcher Setting

        public DirectoryInfo GamePath { get; set; }
        public bool IsDx11 { get; set; }
        public bool AutologinEnabled { get; set; }
        public bool NeedsOtp { get; set; }

        private List<AddonEntry> _internalAddonList;
        [JsonIgnore]
        public List<AddonEntry> AddonList
        {
            get => EnsureDefaultAddon(_internalAddonList);
            set => _internalAddonList = value;
        } 

        public List<UniqueIdCacheEntry> UniqueIdCache { get; set; }
        public bool UniqueIdCacheEnabled { get; set; }
        public bool CharacterSyncEnabled { get; set; }
        public string AdditionalLaunchArgs { get; set; }
        public bool InGameAddonEnabled { get; set; }
        public bool SteamIntegrationEnabled { get; set; }

        
        private ClientLanguage _internalLang;
        [JsonIgnore]
        public ClientLanguage Language
        {
            get => _internalLang;
            set
            {
                if (_internalLang != value)
                    LanguageChanged?.Invoke();

                _internalLang = value;
            }
        }

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

        public static Settings Load()
        {
            return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(ConfigPath), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });
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
