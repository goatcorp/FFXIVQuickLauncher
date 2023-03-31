using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XIVLauncher.Common.Dalamud
{
    internal class DalamudVersionInfo
    {
        [JsonPropertyName("assemblyVersion")]
        public string AssemblyVersion { get; set; }
        
        [JsonPropertyName("supportedGameVer")]
        public string SupportedGameVer { get; set; }
        
        [JsonPropertyName("runtimeVersion")]
        public string RuntimeVersion { get; set; }
        
        [JsonPropertyName("runtimeRequired")]
        public bool RuntimeRequired { get; set; }
        
        [JsonPropertyName("key")]
        public string Key { get; set; }
        
        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; }

        public static DalamudVersionInfo Load(FileInfo file) =>
            JsonSerializer.Deserialize<DalamudVersionInfo>(File.ReadAllText(file.FullName));
    }
}