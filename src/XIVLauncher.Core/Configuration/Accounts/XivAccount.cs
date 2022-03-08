using Newtonsoft.Json;
using Serilog;
using KeySharp;
using Newtonsoft.Json.Linq;

namespace XIVLauncher.Core.Configuration.Accounts
{
    public class XivAccount
    {
        [JsonIgnore]
        public string Id => $"{UserName}-{UseOtp}-{UseSteamServiceAccount}";

        public override string ToString() => Id;

        public string UserName { get; private set; }

        private const string XIVLAUNCHER_PACKAGE = "com.goaaats.xivlauncher";
        private const string XIVLAUNCHER_SERVICE = "FFXIV";

        [JsonIgnore]
        public string Password
        {
            get
            {
                try
                {
                    var credentials = Keyring.GetPassword(XIVLAUNCHER_PACKAGE, XIVLAUNCHER_SERVICE, UserName);
                    return credentials ?? string.Empty;
                }
                catch (KeyringException ex)
                {
                    Log.Error(ex, "Failed to get password for {UserName}", UserName);
                    return string.Empty;
                }
            }
            set
            {
                if (string.IsNullOrEmpty(Password))
                {
                    try
                    {
                        Keyring.DeletePassword(XIVLAUNCHER_PACKAGE, XIVLAUNCHER_SERVICE, UserName);
                    }
                    catch (KeyringException)
                    {
                        // ignored
                    }
                }
                else
                {
                    Keyring.SetPassword(XIVLAUNCHER_PACKAGE, XIVLAUNCHER_SERVICE, UserName, value);
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

        public async Task<string?> FindCharacterThumbUrl()
        {
            if (string.IsNullOrEmpty(ChosenCharacterName) || string.IsNullOrEmpty(ChosenCharacterWorld))
                return null;

            try
            {
                dynamic searchResponse = await GetCharacterSearch(ChosenCharacterName, ChosenCharacterWorld).ConfigureAwait(true);

                if (searchResponse.Results.Count > 1) //If we get more than one match from XIVAPI
                {
                    foreach (var accountInfo in searchResponse.Results)
                    {
                        //We have to check with it all lower in case they type their character name LiKe ThIsLoL. The server XIVAPI returns also contains the DC name, so let's just do a contains on the server to make it easy.
                        if (accountInfo.Name.Value.ToLower() == ChosenCharacterName.ToLower() && accountInfo.Server.Value.ToLower().Contains(ChosenCharacterWorld.ToLower()))
                        {
                            return accountInfo.Avatar.Value;
                        }
                    }
                }

                return searchResponse.Results.Count > 0 ? (string)searchResponse.Results[0].Avatar : null;
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Couldn't download character search");

                return null;
            }
        }

        private const string XIVAPI_BASE = "http://xivapi.com/";

        public static async Task<JObject> GetCharacterSearch(string name, string world)
        {
            return await Get("character/search" + $"?name={name}&server={world}").ConfigureAwait(true);
        }

        public static async Task<dynamic> Get(string endpoint)
        {
            using var client = new HttpClient();

            var result = await client.GetStringAsync(XIVAPI_BASE + endpoint).ConfigureAwait(true);

            var parsedObject = JObject.Parse(result);

            return parsedObject;
        }
    }
}