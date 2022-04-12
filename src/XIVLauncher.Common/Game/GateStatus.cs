using System.Collections.Generic;
using Newtonsoft.Json;

namespace XIVLauncher.Common.Game;

public class GateStatus
{
    [JsonProperty("status")]
    public bool Status { get; set; }

    [JsonProperty("message")]
    public List<string> Message { get; set; }

    [JsonProperty("news")]
    public List<string> News { get; set; }
}