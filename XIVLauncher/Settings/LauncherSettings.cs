using Dalamud;
using Dalamud.Discord;
using Newtonsoft.Json;
using Serilog;
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
        public string CurrentAccountId { get; set; }

        #endregion

        #region SaveLoad

        private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "launcherConfig.json");

        public void Save()
        {
            Log.Information("Saving LauncherSettings to {0}", ConfigPath);

            File.WriteAllText(ConfigPath,  JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
            }));
        }

        public static LauncherSettings Load()
        {
            if (!File.Exists(ConfigPath))
            {
                Log.Information("LauncherSettings at {0} does not exist, creating new...", ConfigPath);
                return new LauncherSettings();
            }

            var setting = JsonConvert.DeserializeObject<LauncherSettings>(File.ReadAllText(ConfigPath), new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });

            setting.AddonList = EnsureDefaultAddon(setting.AddonList);

            Log.Information("Loaded LauncherSettings at {0}", ConfigPath);

            return setting;
        }

        public static void TryMigrate(ILauncherSettingsV3 newSetting)
        {
            if (!File.Exists(ConfigPath))
                return;

            var oldSetting = Load();

            newSetting.AdditionalLaunchArgs = oldSetting.AdditionalLaunchArgs;
            newSetting.AutologinEnabled = oldSetting.AutologinEnabled;
            newSetting.CharacterSyncEnabled = oldSetting.CharacterSyncEnabled;
            newSetting.GamePath = oldSetting.GamePath;
            newSetting.InGameAddonEnabled = oldSetting.InGameAddonEnabled;
            newSetting.CurrentAccountId = oldSetting.CurrentAccountId;
            newSetting.SteamIntegrationEnabled = oldSetting.SteamIntegrationEnabled;
            newSetting.IsDx11 = oldSetting.IsDx11;
            newSetting.Language = oldSetting.Language;
            newSetting.UniqueIdCacheEnabled = oldSetting.UniqueIdCacheEnabled;
            newSetting.AddonList = oldSetting.AddonList;

            File.Delete(ConfigPath);
        }

        private static List<AddonEntry> EnsureDefaultAddon(List<AddonEntry> addonList)
        {
            if (addonList == null)
                addonList = new List<AddonEntry>();

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
    }
}
