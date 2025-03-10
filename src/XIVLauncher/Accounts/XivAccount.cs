using AdysTech.CredentialManager;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Net;

namespace XIVLauncher.Accounts
{
    public class XivAccount
    {
        private const string CREDS_PREFIX_OLD = "FINAL FANTASY XIV";
        private const string CREDS_PREFIX_NEW = "XIVLAUNCHER";

        [JsonIgnore]
        public string Id => $"{UserName}-{UseOtp}-{UseSteamServiceAccount}";

        public override string ToString() => Id;

        public string UserName { get; private set; }

        [JsonIgnore]
        public string Password
        {
            get
            {
                var credentials = CredentialManager.GetCredentials($"{CREDS_PREFIX_OLD}-{UserName.ToLower()}");

                if (credentials != null)
                {
                    var saved = CredentialManager.SaveCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}", new NetworkCredential
                    {
                        UserName = credentials.UserName,
                        Password = credentials.Password,
                    });

                    if (saved)
                    {
                        try
                        {
                            CredentialManager.RemoveCredentials($"{CREDS_PREFIX_OLD}-{UserName.ToLower()}");
                        }
                        catch (Win32Exception)
                        {
                            // ignored
                        }
                    }
                }
                else
                {
                    credentials = CredentialManager.GetCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}");
                }

                return credentials != null ? credentials.Password : string.Empty;
            }
            set
            {
                try
                {
                    CredentialManager.RemoveCredentials($"{CREDS_PREFIX_OLD}-{UserName.ToLower()}");
                }
                catch (Win32Exception)
                {
                    // ignored
                }

                try
                {
                    CredentialManager.RemoveCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}");
                }
                catch (Win32Exception)
                {
                    // ignored
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    CredentialManager.SaveCredentials($"{CREDS_PREFIX_NEW}-{UserName.ToLower()}", new NetworkCredential
                    {
                        UserName = UserName,
                        Password = value
                    });
                }
            }
        }

        public bool SavePassword { get; set; }
        public bool UseSteamServiceAccount { get; set; }
        public bool UseOtp { get; set; }

        public string ChosenCharacterName;
        public string ChosenCharacterWorld;

        public string ThumbnailUrl;

        public string LastSuccessfulOtp;

        public XivAccount(string userName)
        {
            UserName = userName.ToLower();
        }

        public string FindCharacterThumb()
        {
            if (string.IsNullOrEmpty(ChosenCharacterName) || string.IsNullOrEmpty(ChosenCharacterWorld))
                return null;

            // STUB
            return null;
        }
    }
}
