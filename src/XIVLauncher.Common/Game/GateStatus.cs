using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XIVLauncher.Common.Game;

public class GateStatus
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("message")]
    public List<string> Message { get; set; }

    [JsonPropertyName("news")]
    public List<string> News { get; set; }
}