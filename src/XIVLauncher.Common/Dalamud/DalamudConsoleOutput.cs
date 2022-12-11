using System.Text.Json.Serialization;

namespace XIVLauncher.Common.Dalamud
{
    public sealed class DalamudConsoleOutput
    {
        [JsonPropertyName("pid")]
        public int Pid { get; set; }

        [JsonPropertyName("handle")]
        public long Handle { get; set; }
    }
}