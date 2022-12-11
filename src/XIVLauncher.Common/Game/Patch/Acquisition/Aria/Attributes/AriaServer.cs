/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace AriaNet.Attributes
{
    public class ServerDetail
    {
        [JsonPropertyName("currentUri")]
        public string CurrentUri { get; set; }

        [JsonPropertyName("downloadSpeed")]
        public string DownloadSpeed { get; set; }

        [JsonPropertyName("uri")]
        public string Uri { get; set; }
    }

    public class AriaServer
    {
        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("servers")]
        public List<ServerDetail> Servers { get; set; }
    }
}
