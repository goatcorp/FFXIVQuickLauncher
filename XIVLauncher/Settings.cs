using Dalamud;
using Dalamud.Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XIVLauncher.Addon;
using XIVLauncher.Cache;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;

namespace XIVLauncher
{
    // TODO: All of this needs a rework
    static class Settings
    {
        public static Action LanguageChanged;

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

        public static void SetLanguage(ClientLanguage language)
        {
            int previousLanguage = Properties.Settings.Default.Language;
            Properties.Settings.Default.Language = (int)language;

            if (previousLanguage != (int)language)
                LanguageChanged?.Invoke();
        }

        public static bool IsDX11()
        {
            return Properties.Settings.Default.IsDx11;
        }

        public static void SetDx11(bool value)
        {
            Properties.Settings.Default.IsDx11 = value;
        }

        public static bool IsAutologin()
        {
            return Properties.Settings.Default.AutoLogin;
        }

        public static void SetAutologin(bool value)
        {
            Properties.Settings.Default.AutoLogin = value;
        }

        public static bool NeedsOtp()
        {
            return Properties.Settings.Default.NeedsOtp;
        }

        public static void SetNeedsOtp(bool value)
        {
            Properties.Settings.Default.NeedsOtp = value;
        }

        public static List<AddonEntry> GetAddonList()
        {
            var addonList = Properties.Settings.Default.Addons;

            var list = JsonConvert.DeserializeObject<List<AddonEntry>>(addonList, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });

            list = EnsureDefaultAddons(list);

            return list;
        }

        public static void SetAddonList(List<AddonEntry> list)
        {
            Properties.Settings.Default.Addons = JsonConvert.SerializeObject(list, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects,
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple
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

        public static bool RmtFilterEnabled
        {
            get => Properties.Settings.Default.RmtFilterEnabled;
            set => Properties.Settings.Default.RmtFilterEnabled = value;
        }

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

        public static bool CharacterSyncEnabled
        {
            get => Properties.Settings.Default.CharacterSyncEnabled;
            set => Properties.Settings.Default.CharacterSyncEnabled = value;
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

        public static bool IsInGameAddonEnabled()
        {
            return Properties.Settings.Default.InGameAddonEnabled;
        }

        public static void SetInGameAddonEnabled(bool value)
        {
            Properties.Settings.Default.InGameAddonEnabled = value;
        }

        public static bool SteamIntegrationEnabled
        {
            get => Properties.Settings.Default.SteamIntegrationEnabled;
            set => Properties.Settings.Default.SteamIntegrationEnabled = value;
        }

        public static void Save()
        {
            Properties.Settings.Default.Save();
        }

        private static List<AddonEntry> EnsureDefaultAddons(List<AddonEntry> addonList)
        {
            if (!addonList.Any(entry => entry.Addon is RichPresenceAddon))
            {
                addonList.Add(new AddonEntry
                {
                    Addon = new RichPresenceAddon(),
                    IsEnabled = false
                });
            }

            // Mistakes were made
            if (addonList.Any(entry => entry.Addon.GetType() == typeof(DalamudLauncher)))
            {
                var addon = addonList.First(entry => entry.Addon is DalamudLauncher);

                SetInGameAddonEnabled(addon.IsEnabled);

                addonList = addonList.Where(entry => !(entry.Addon is DalamudLauncher)).ToList();
            }

            return addonList;
        }

        public static string AdditionalLaunchArgs
        {
            get => Properties.Settings.Default.AdditionalLaunchArgs;
            set => Properties.Settings.Default.AdditionalLaunchArgs = value;
        }

        public static CustomComboPreset ComboPresets
        {
            get => DalamudConfig.ComboPresets;
            set
            {
                var currentConfig = DalamudConfig;
                currentConfig.ComboPresets = value;
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
                    ComboPresets = CustomComboPreset.None,
                    DiscordFeatureConfig = discordFeatureConfig,
                    BadWords = new List<string>(),
                    Fates = new List<DalamudConfiguration.FateInfo>()
                };

                DalamudConfig = newDalamudConfig;
                return newDalamudConfig;
            }
            set => File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "dalamudConfig.json"), JsonConvert.SerializeObject(value));
        }
    }
}
