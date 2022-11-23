using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game
{
    public partial class Headlines
    {
        [JsonProperty("news")]
        public News[] News { get; set; }

        [JsonProperty("topics")]
        public News[] Topics { get; set; }

        [JsonProperty("pinned")]
        public News[] Pinned { get; set; }

        [JsonProperty("banner")]
        public Banner[] Banner { get; set; }
    }

    public class Banner
    {
        [JsonProperty("lsb_banner")]
        public Uri LsbBanner { get; set; }

        [JsonProperty("link")]
        public Uri Link { get; set; }
    }

    public class News
    {
        [JsonProperty("date")]
        public DateTimeOffset Date { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
        public string Tag { get; set; }
    }

    public partial class Headlines
    {
        public static async Task<Headlines> Get(Launcher game, ClientLanguage language, bool forceNa = false)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var langCode = language.GetLangCode(forceNa);
            var url = $"https://frontier.ffxiv.com/news/headline.json?lang={langCode}&media=pcapp&_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonConvert.DeserializeObject<Headlines>(json, Converter.SETTINGS);
        }
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings SETTINGS = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            }
        };
    }
}