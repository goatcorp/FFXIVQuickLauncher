/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using Newtonsoft.Json;

namespace AriaNet.Attributes
{
    public class AriaTorrent
    {
        [JsonProperty("amChoking")]
        public string AmChoking { get; set; }

        [JsonProperty("bitfield")]
        public string BitField { get; set; }

        [JsonProperty("downloadSpeed")]
        public string DownloadSpeed { get; set; }

        [JsonProperty("ip")]
        public string Ip { get; set; }

        [JsonProperty("peerChoking")]
        public string PeerChoking { get; set; }

        [JsonProperty("peerId")]
        public string PeerId { get; set; }

        [JsonProperty("port")]
        public string Port { get; set; }

        [JsonProperty("seeder")]
        public string Seeder { get; set; }

        [JsonProperty("uploadSpeed")]
        public string UploadSpeed { get; set; }
    }
}
