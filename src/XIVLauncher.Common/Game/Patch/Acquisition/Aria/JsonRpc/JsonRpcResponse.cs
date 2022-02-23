using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace XIVLauncher.Common.Game.Patch.Acquisition.Aria.JsonRpc
{
    public class JsonRpcResponse<T>
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("jsonrpc")]
        public string Version { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }
    }
}
