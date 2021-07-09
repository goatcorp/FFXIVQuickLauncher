/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using Newtonsoft.Json;

namespace AriaNet.Attributes
{
    [JsonObject]
    public class AriaGlobalStatus
    {
        [JsonProperty("downloadSpeed")]
        public int DownloadSpeed { get; set; }
        
        [JsonProperty("numActive")]
        public int ActiveTaskCount { get; set; }
        
        [JsonProperty("numStopped")]
        public int StoppedTaskCount { get; set; }
        
        [JsonProperty("numWaiting")]
        public int WaitingTaskCount { get; set; }
        
        [JsonProperty("uploadSpeed")]
        public int UploadSpeed { get; set; }
    }
}