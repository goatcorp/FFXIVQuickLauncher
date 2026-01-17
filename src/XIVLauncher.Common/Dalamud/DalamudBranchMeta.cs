using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Dalamud;

public static class DalamudBranchMeta
{
    public class Branch
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("track")]
        public string Track { get; set; }

        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("assemblyVersion")]
        public string AssemblyVersion { get; set; }

        [JsonPropertyName("runtimeVersion")]
        public string RuntimeVersion { get; set; }

        [JsonPropertyName("runtimeRequired")]
        public bool RuntimeRequired { get; set; }

        [JsonPropertyName("supportedGameVer")]
        public string SupportedGameVer { get; set; }

        [JsonPropertyName("isApplicableForCurrentGameVer")]
        public bool? IsApplicableForCurrentGameVer { get; set; }

        public string DisplayNameWithAvailability => !this.IsApplicableForCurrentGameVer.GetValueOrDefault(false) ? $"{this.DisplayName} (unavailable)" : this.DisplayName;
    }

    public static async Task<IEnumerable<Branch>> FetchBranchesAsync(HttpClient client)
    {
        var json = await client.GetStringAsync("https://kamori.goats.dev/Dalamud/Release/Meta");
        var dict = JsonSerializer.Deserialize<Dictionary<string, Branch>>(json);
        return dict == null ? throw new Exception("Failed to deserialize branch metadata.") : dict.Values;
    }
}
