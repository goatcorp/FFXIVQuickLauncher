using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AdysTech.CredentialManager;
using Dalamud.Discord;
using Newtonsoft.Json;
using XIVLauncher.Addon;
using XIVLauncher.Cache;

namespace XIVLauncher
{
    // TODO: All of this needs a rework
    static class Settings
    {
        public static Action LanguageChanged;

        public static NetworkCredential GetCredentials(string app)
        {
            return CredentialManager.GetCredentials(app);
        }

        public static void SaveCredentials(string app, string username, string password)
        {
            CredentialManager.SaveCredentials(app, new NetworkCredential(username, password));
        }

        public static void ResetCredentials(string app)
        {
            if (CredentialManager.GetCredentials(app) != null)
            {
                CredentialManager.RemoveCredentials(app);
            }
        }

        public static string GetGamePath()
        {
            return Properties.Settings.Default.GamePath;
        }

        public static void SetGamePath(string path)
        {
            Properties.Settings.Default.GamePath = path;
        }

        public static ClientLanguage GetLanguage()
        {
            return (ClientLanguage) Properties.Settings.Default.Language;
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

        public static DiscordFeatureConfiguration DiscordFeatureConfig
        {
            get
            {
                var config = JsonConvert.DeserializeObject<DiscordFeatureConfiguration>(Properties.Settings.Default
                    .DiscordFeatureConfiguration);;

                return config ?? new DiscordFeatureConfiguration
                {
                    ChatTypeConfigurations = new List<ChatTypeConfiguration>()
                };
            }

            set => Properties.Settings.Default.DiscordFeatureConfiguration = JsonConvert.SerializeObject(value);
        }

        public static bool IsInGameAddonEnabled()
        {
            return Properties.Settings.Default.InGameAddonEnabled;
        }

        public static void SetInGameAddonEnabled(bool value)
        {
            Properties.Settings.Default.InGameAddonEnabled = value;
        }

        public static void Save()
        {
            Properties.Settings.Default.Save();
        }

        private static List<AddonEntry> EnsureDefaultAddons(List<AddonEntry> addonList)
        {
            if (addonList.All(entry => entry.Addon.GetType() != typeof(RichPresenceAddon)))
            {
                addonList.Add(new AddonEntry
                {
                    Addon = new RichPresenceAddon(),
                    IsEnabled = false
                });
            }

            // Mistakes were made
            if (addonList.Any(entry => entry.Addon.GetType() == typeof(HooksAddon)))
            {
                var addon = addonList.First(entry => entry.Addon is HooksAddon);

                SetInGameAddonEnabled(addon.IsEnabled);

                addonList = addonList.Where(entry => entry.Addon is HooksAddon).ToList();

                addonList = addonList.Where(entry => !(entry.Addon is GenericAddon genericAddon) || !string.IsNullOrEmpty(genericAddon.Path)).ToList();
            }

            return addonList;
        }
    }
}
