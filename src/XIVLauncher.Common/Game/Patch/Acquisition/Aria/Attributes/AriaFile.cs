/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AriaNet.Attributes
{
    public class AriaFile
    {
        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("length")]
        public string Length { get; set; }

        [JsonPropertyName("completedLength")]
        public string CompletedLength { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }

        [JsonPropertyName("selected")]
        public string Selected { get; set; }

        [JsonPropertyName("uris")]
        public List<AriaUri> Uris { get; set; }
    }
}
