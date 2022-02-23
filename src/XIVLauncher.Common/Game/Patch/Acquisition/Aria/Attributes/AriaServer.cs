/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Collections.Generic;
using Newtonsoft.Json;


namespace AriaNet.Attributes
{
    public class ServerDetail
    {
        [JsonProperty("currentUri")]
        public string CurrentUri { get; set; }

        [JsonProperty("downloadSpeed")]
        public string DownloadSpeed { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }
    }

    public class AriaServer
    {
        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("servers")]
        public List<ServerDetail> Servers { get; set; }
    }
}
