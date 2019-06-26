using System;
using System.Collections.Generic;

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace XIVLauncher
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

    public partial class Banner
    {
        [JsonProperty("lsb_banner")]
        public Uri LsbBanner { get; set; }

        [JsonProperty("link")]
        public Uri Link { get; set; }
    }

    public partial class News
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
        public static Headlines Get(XIVGame game)
        {
            var unixTimestamp = Util.GetUnixMillis();
            var langCode = Settings.GetLanguage().GetLangCode();
            var url = $"https://frontier.ffxiv.com/news/headline.json?lang={langCode}&media=pcapp&{unixTimestamp}";

            var json = Encoding.UTF8.GetString(game.DownloadAsLauncher(url));

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
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }
}
