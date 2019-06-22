using System;
using System.Collections.Generic;

using System.Globalization;
using System.Net;
using System.Net.Http;
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
        public static Headlines Get()
        {
            using (var client = new WebClient())
            {
                string url;
                switch (Settings.GetLanguage())
                {
                    case ClientLanguage.Japanese:
                        url = "https://frontier.ffxiv.com/news/headline.json?lang=ja&media=pcapp&1552178812383";
                        break;

                    case ClientLanguage.English:
                        url = "https://frontier.ffxiv.com/news/headline.json?lang=en-gb&media=pcapp&1552178812383";
                        break;

                    case ClientLanguage.German:
                        url = "https://frontier.ffxiv.com/news/headline.json?lang=de&media=pcapp&1552178812383";
                        break;

                    case ClientLanguage.French:
                        url = "https://frontier.ffxiv.com/news/headline.json?lang=fr&media=pcapp&1552178812383";
                        break;

                    default:
                        url = "https://frontier.ffxiv.com/news/headline.json?lang=en-gb&media=pcapp&1552178812383";
                        break;
                }
                var json = client.DownloadString(
                    new Uri(url));

                return JsonConvert.DeserializeObject<Headlines>(json, Converter.Settings);
            }
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
