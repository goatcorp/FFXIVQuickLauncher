using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XIVLauncher.Addon.Implementations.XivRichPresence
{
    public static class XivApi
    {
        private const string URL = "http://xivapi.com/";

        private static readonly Dictionary<string, JObject> CachedRequests = new Dictionary<string, JObject>();

        public static async Task<string> GetNameForWorld(int world)
        {
            var res = await Get("World/" + world);

            return res.Name;
        }

        public static async Task<int> GetLoadingImageKeyForTerritoryType(int territoryType)
        {
            var res = await Get("TerritoryType/" + territoryType);

            try
            {
                return (int) res.LoadingImageTargetID;
            }
            catch (RuntimeBinderException)
            {
                return 1;
            }
        }

        public static async Task<string> GetPlaceNameZoneForTerritoryType(int territoryType)
        {
            var res = await Get("TerritoryType/" + territoryType);

            try
            {
                return (string) res.PlaceNameZone.Name_en;
            }
            catch (RuntimeBinderException)
            {
                return "Not Found";
            }
        }

        public static async Task<string> GetPlaceNameForTerritoryType(int territoryType)
        {
            var res = await Get("TerritoryType/" + territoryType);

            try
            {
                return (string) res.PlaceName.Name_en;
            }
            catch (RuntimeBinderException)
            {
                return "Not Found";
            }
        }

        public static async Task<string> GetJobName(int jobId)
        {
            var res = await Get("ClassJob/" + jobId);

            return res.NameEnglish_en;
        }

        public static async Task<JObject> GetCharacterSearch(string name, string world)
        {
            return await Get("character/search" + $"?name={name}&server={world}");
        }

        public static async Task<dynamic> Get(string endpoint)
        {
            if (CachedRequests.ContainsKey(endpoint))
                return CachedRequests[endpoint];

            using (var client = new WebClient())
            {
                var result = client.DownloadString(URL + endpoint);

                var parsedObject = JObject.Parse(result);
                CachedRequests.Add(endpoint, parsedObject);

                return parsedObject;
            }
        }
    }
}
