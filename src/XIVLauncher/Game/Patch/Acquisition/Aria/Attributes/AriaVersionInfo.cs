/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Collections.Generic;
using Newtonsoft.Json;

namespace AriaNet.Attributes
{
    [JsonObject]
    public class AriaVersionInfo
    {
        [JsonProperty("enabledFeatures")]
        public List<string> EnabledFeatures { get; set; }
        
        [JsonProperty("version")]
        public string Version { get; set; }
    }
}