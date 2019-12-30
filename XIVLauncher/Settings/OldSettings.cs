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
    // TODO: All of this needs a rework
    static class OldSettings
    {
        #region Old Setting

        public static DirectoryInfo GamePath
        {
            get
            {
                if (string.IsNullOrEmpty(Properties.Settings.Default.GamePath))
                    return null;

                return new DirectoryInfo(Properties.Settings.Default.GamePath);
            }
            set => Properties.Settings.Default.GamePath = value?.FullName;
        }

        public static ClientLanguage GetLanguage()
        {
            return (ClientLanguage)Properties.Settings.Default.Language;
        }

        public static bool IsDX11()
        {
            return Properties.Settings.Default.IsDx11;
        }

        public static bool IsAutologin()
        {
            return Properties.Settings.Default.AutoLogin;
        }

        public static bool NeedsOtp()
        {
            return Properties.Settings.Default.NeedsOtp;
        }

        public static List<AddonEntry> GetAddonList()
        {
            var addonList = Properties.Settings.Default.Addons;

            return JsonConvert.DeserializeObject<List<AddonEntry>>(addonList, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });
        }

        public static List<UniqueIdCacheEntry> UniqueIdCache
        {
            get
            {
                var cache = JsonConvert.DeserializeObject<List<UniqueIdCacheEntry>>(Properties.Settings.Default
                    .UniqueIdCache);

                return cache ?? new List<UniqueIdCacheEntry>();
            }

            set => Properties.Settings.Default.UniqueIdCache = JsonConvert.SerializeObject(value);
        }

        public static bool UniqueIdCacheEnabled
        {
            get => Properties.Settings.Default.UniqueIdCacheEnabled;
            set => Properties.Settings.Default.UniqueIdCacheEnabled = value;
        }

        public static bool CharacterSyncEnabled
        {
            get => Properties.Settings.Default.CharacterSyncEnabled;
            set => Properties.Settings.Default.CharacterSyncEnabled = value;
        }

        public static bool IsInGameAddonEnabled()
        {
            return Properties.Settings.Default.InGameAddonEnabled;
        }

        public static bool SteamIntegrationEnabled
        {
            get => Properties.Settings.Default.SteamIntegrationEnabled;
            set => Properties.Settings.Default.SteamIntegrationEnabled = value;
        }
        
        public static string AdditionalLaunchArgs
        {
            get => Properties.Settings.Default.AdditionalLaunchArgs;
            set => Properties.Settings.Default.AdditionalLaunchArgs = value;
        }

        #endregion

        public static LauncherSettings Migrate()
        {
            var newSetting = LauncherSettings.Load();

            newSetting.AdditionalLaunchArgs = AdditionalLaunchArgs;
            newSetting.AddonList = GetAddonList();
            newSetting.AutologinEnabled = IsAutologin();
            newSetting.CharacterSyncEnabled = CharacterSyncEnabled;
            newSetting.GamePath = GamePath;
            newSetting.InGameAddonEnabled = IsInGameAddonEnabled();
            newSetting.IsDx11 = IsDX11();
            newSetting.Language = GetLanguage();
            newSetting.NeedsOtp = NeedsOtp();
            newSetting.UniqueIdCacheEnabled = UniqueIdCacheEnabled;
            newSetting.SteamIntegrationEnabled = SteamIntegrationEnabled;

            newSetting.Save();

            return newSetting;
        }
    }
}
