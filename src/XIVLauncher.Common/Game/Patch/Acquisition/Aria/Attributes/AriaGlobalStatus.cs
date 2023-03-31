/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Text.Json.Serialization;

namespace AriaNet.Attributes
{
    public class AriaGlobalStatus
    {
        [JsonPropertyName("downloadSpeed")]
        public int DownloadSpeed { get; set; }

        [JsonPropertyName("numActive")]
        public int ActiveTaskCount { get; set; }

        [JsonPropertyName("numStopped")]
        public int StoppedTaskCount { get; set; }

        [JsonPropertyName("numWaiting")]
        public int WaitingTaskCount { get; set; }

        [JsonPropertyName("uploadSpeed")]
        public int UploadSpeed { get; set; }
    }
}