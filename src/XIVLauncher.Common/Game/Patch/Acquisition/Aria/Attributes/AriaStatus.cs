/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AriaNet.Attributes
{
    public class AriaStatus
    {

        [JsonPropertyName("bitfield")]
        public string Bitfield { get; set; }

        [JsonPropertyName("completedLength")]
        public string CompletedLength { get; set; }

        [JsonPropertyName("connections")]
        public string Connections { get; set; }

        [JsonPropertyName("dir")]
        public string Dir { get; set; }

        [JsonPropertyName("downloadSpeed")]
        public string DownloadSpeed { get; set; }

        [JsonPropertyName("files")]
        public List<AriaFile> Files { get; set; }

        [JsonPropertyName("gid")]
        public string TaskId { get; set; }

        [JsonPropertyName("numPieces")]
        public string NumPieces { get; set; }

        [JsonPropertyName("pieceLength")]
        public string PieceLength { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("totalLength")]
        public string TotalLength { get; set; }

        [JsonPropertyName("uploadLength")]
        public string UploadLength { get; set; }

        [JsonPropertyName("uploadSpeed")]
        public string UploadSpeed { get; set; }
    }
}
