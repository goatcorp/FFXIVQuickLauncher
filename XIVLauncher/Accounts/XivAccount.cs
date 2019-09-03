using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using AdysTech.CredentialManager;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Addon.Implementations.XivRichPresence;

namespace XIVLauncher.Accounts
{
    public class XivAccount
    {
        [JsonIgnore]
        public string Id => $"{UserName}-{UseOtp}-{UseSteamServiceAccount}";

        public string UserName { get; private set; }

        [JsonIgnore]
        public string Password
        {
            get
            {
                var credentials = CredentialManager.GetCredentials($"FINAL FANTASY XIV-{UserName}");

                return credentials != null ? credentials.Password : string.Empty;
            }
            set => CredentialManager.SaveCredentials($"FINAL FANTASY XIV-{UserName}", new NetworkCredential
                {
                    UserName = UserName,
                    Password = value
                });
        }

        public bool SavePassword { get; set; }
        public bool UseSteamServiceAccount { get; set; }
        public bool UseOtp { get; set; }

        public string ChosenCharacterName;
        public string ChosenCharacterWorld;

        public string ThumbnailUrl;

        public XivAccount(string userName)
        {
            UserName = userName;
        }

        public string FindCharacterThumb()
        {
            if (string.IsNullOrEmpty(ChosenCharacterName) || string.IsNullOrEmpty(ChosenCharacterWorld))
                return null;

            try
            {
                dynamic searchResponse = XivApi.GetCharacterSearch(ChosenCharacterName, ChosenCharacterWorld)
                    .GetAwaiter().GetResult();

                return searchResponse.Results.Count > 0 ? (string) searchResponse.Results[0].Avatar : null;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Couldn't download character search.");

                return null;
            }
        }
    }
}
