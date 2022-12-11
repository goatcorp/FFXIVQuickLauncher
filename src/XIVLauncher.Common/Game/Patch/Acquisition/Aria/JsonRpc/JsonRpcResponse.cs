using System.Text.Json.Serialization;

namespace XIVLauncher.Common.Game.Patch.Acquisition.Aria.JsonRpc
{
    public class JsonRpcResponse<T>
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("jsonrpc")]
        public string Version { get; set; }

        [JsonPropertyName("result")]
        public T Result { get; set; }
    }
}
