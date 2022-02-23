using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;

namespace XIVLauncher.Common.Game.Patch.Acquisition.Aria.JsonRpc
{
    /// <summary>
    /// Bodge JSON-RPC 2.0 http client implementation
    /// </summary>
    public class JsonRpcHttpClient
    {
        private readonly string _endpoint;
        private readonly HttpClient _client;

        public JsonRpcHttpClient(string endpoint)
        {
            _endpoint = endpoint;
            _client = new HttpClient
            {
                Timeout = new TimeSpan(0, 5, 0)
            };
        }

        private static string Base64Encode(string plainText) {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public async Task<T> Invoke<T>(string method, params object[] args)
        {
            var argsJson = JsonConvert.SerializeObject(args);
            Log.Debug($"[JSONRPC] method({method}) arg({argsJson})");

            var httpResponse = await _client.GetAsync(_endpoint + $"?method={method}&id={Guid.NewGuid()}&params={Base64Encode(argsJson)}");
            httpResponse.EnsureSuccessStatusCode();

            var rpcResponse = JsonConvert.DeserializeObject<JsonRpcResponse<T>>(await httpResponse.Content.ReadAsStringAsync());
            return rpcResponse.Result;
        }
    }
}
