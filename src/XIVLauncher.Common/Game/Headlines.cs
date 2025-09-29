using System;
using System.Collections.Generic;
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
    }

    public class Banner
    {
        [JsonProperty("lsb_banner")]
        public Uri LsbBanner { get; set; }

        [JsonProperty("link")]
        public Uri Link { get; set; }

        [JsonProperty("order_priority")]
        public int? OrderPriority { get; set; }

        [JsonProperty("fix_order")]
        public int? FixOrder { get; set; }
    }

    public class BannerRoot
    {
        [JsonProperty("banner")]
        public List<Banner> Banner { get; set; }
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
        public static async Task<Headlines> GetNews(Launcher game, ClientLanguage language, bool forceNa = false)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var langCode = language.GetLangCode(forceNa);
            var url = $"https://frontier.ffxiv.com/news/headline.json?lang={langCode}&media=pcapp&_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            var news = JsonConvert.DeserializeObject<Headlines>(json, Converter.SETTINGS);
            foreach (var item in news.News)
            {
                if (string.IsNullOrEmpty(item.Url) && !string.IsNullOrEmpty(item.Id))
                {
                    item.Url = $"https://{language.GetLangCodeLodestone()}.finalfantasyxiv.com/lodestone/news/detail/{item.Id}";
                }
            }
            foreach (var item in news.Topics)
            {
                if (string.IsNullOrEmpty(item.Url) && !string.IsNullOrEmpty(item.Id))
                {
                    item.Url = $"https://{language.GetLangCodeLodestone()}.finalfantasyxiv.com/lodestone/topics/detail/{item.Id}";
                }
            }
            return news;
        }

        public static async Task<IReadOnlyList<Banner>> GetBanners(Launcher game, ClientLanguage language, bool forceNa = false)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var langCode = language.GetLangCode(forceNa);
            var url = $"https://frontier.ffxiv.com/v2/topics/{langCode}/banner.json?lang={langCode}&media=pcapp&_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonConvert.DeserializeObject<BannerRoot>(json, Converter.SETTINGS).Banner;
        }

        public static async Task<IReadOnlyCollection<Banner>> GetMessage(Launcher game, ClientLanguage language, bool forceNa = false)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var langCode = language.GetLangCode(forceNa);
            var url = $"https://frontier.ffxiv.com/v2/notice/{langCode}/message.json?_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonConvert.DeserializeObject<BannerRoot>(json, Converter.SETTINGS).Banner;
        }

        public static async Task<IReadOnlyCollection<Banner>> GetWorlds(Launcher game, ClientLanguage language)
        {
            var unixTimestamp = ApiHelpers.GetUnixMillis();
            var url = $"https://frontier.ffxiv.com/v2/world/status.json?_={unixTimestamp}";

            var json = Encoding.UTF8.GetString(await game.DownloadAsLauncher(url, language, "application/json, text/plain, */*").ConfigureAwait(false));

            return JsonConvert.DeserializeObject<BannerRoot>(json, Converter.SETTINGS).Banner;
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
