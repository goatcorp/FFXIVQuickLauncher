/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Text.Json.Serialization;

namespace AriaNet.Attributes
{
    public class AriaTorrent
    {
        [JsonPropertyName("amChoking")]
        public string AmChoking { get; set; }

        [JsonPropertyName("bitfield")]
        public string BitField { get; set; }

        [JsonPropertyName("downloadSpeed")]
        public string DownloadSpeed { get; set; }

        [JsonPropertyName("ip")]
        public string Ip { get; set; }

        [JsonPropertyName("peerChoking")]
        public string PeerChoking { get; set; }

        [JsonPropertyName("peerId")]
        public string PeerId { get; set; }

        [JsonPropertyName("port")]
        public string Port { get; set; }

        [JsonPropertyName("seeder")]
        public string Seeder { get; set; }

        [JsonPropertyName("uploadSpeed")]
        public string UploadSpeed { get; set; }
    }
}
