using System.Threading.Tasks;
using StreamJsonRpc;
using XIVLauncher.Common.Dalamud.Rpc.Types;

namespace XIVLauncher.Common.Dalamud.Rpc;

// WARNING: Do not alter this file without coordinating with the Dalamud team.

public interface IDalamudRpc
{
    [JsonRpcMethod("hello")]
    Task<ClientHelloResponse> HelloAsync(ClientHelloRequest request);

    [JsonRpcMethod("handleLink")]
    Task HandleLinkAsync(string link);
}
