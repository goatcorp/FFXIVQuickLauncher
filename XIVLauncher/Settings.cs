using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using AdysTech.CredentialManager;
using Newtonsoft.Json;
using XIVLauncher.Addon;

namespace XIVLauncher
{
    static class Settings
    {
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
            Properties.Settings.Default.Language = (int) language;
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

        public static int GetExpansionLevel()
        {
            return Properties.Settings.Default.ExpansionLevel;
        }

        public static void SetExpansionLevel(int level)
        {
            Properties.Settings.Default.ExpansionLevel = level;
        }

        public static List<AddonEntry> GetAddonList()
        {
            var addonList = Properties.Settings.Default.Addons;

            if(string.IsNullOrEmpty(addonList))
                return new List<AddonEntry>()
                {
                    new AddonEntry()
                    {
                        Addon = new RichPresenceAddon(),
                        IsEnabled = false
                    }
                };

            var list = JsonConvert.DeserializeObject<List<AddonEntry>>(addonList, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Objects
            });

            if(list.Count == 0)
                list.Add(new AddonEntry()
                {
                    Addon = new RichPresenceAddon(),
                    IsEnabled = false
                });

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

        public static void Save()
        {
            Properties.Settings.Default.Save();
        }

        public static bool IsAdministrator()
        {
            return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                    .IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
