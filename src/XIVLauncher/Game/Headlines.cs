using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace XIVLauncher.Game
{
    public partial class Headlines
    {
        [JsonProperty("news")] public News[] News { get; set; }

        [JsonProperty("topics")] public News[] Topics { get; set; }

        [JsonProperty("pinned")] public News[] Pinned { get; set; }

        [JsonProperty("banner")] public Banner[] Banner { get; set; }
    }

    public class Banner
    {
        [JsonProperty("lsb_banner")] public Uri LsbBanner { get; set; }

        [JsonProperty("link")] public Uri Link { get; set; }
    }

    public class News
    {
        [JsonProperty("date")] public DateTimeOffset Date { get; set; }

        [JsonProperty("title")] public string Title { get; set; }

        [JsonProperty("url")] public string Url { get; set; }

        [JsonProperty("id")] public string Id { get; set; }

        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
        public string Tag { get; set; }
    }

    public partial class Headlines
    {
        public static async Task<Headlines> Get(Launcher game, ClientLanguage language)
        {
            var unixTimestamp = Util.GetUnixMillis();
            var langCode = language.GetLangCode();
            var url = $"https://frontier.ffxiv.com/news/headline.json?lang={langCode}&media=pcapp&{unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*"));

            return JsonConvert.DeserializeObject<Headlines>(json, Converter.Settings);
        }
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.AssumeUniversal}
            }
        };
    }
}