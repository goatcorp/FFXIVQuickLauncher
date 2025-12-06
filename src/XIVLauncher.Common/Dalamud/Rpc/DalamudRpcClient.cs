using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace XIVLauncher.Common.Dalamud.Rpc;

public class DalamudRpcClient : IAsyncDisposable
{
    private readonly Socket socket;
    private readonly Stream stream;
    private readonly JsonRpc rpc;

    public IDalamudRpc Proxy { get; }

    public string SocketPath { get; }

    private DalamudRpcClient(string socketPath, Socket socket)
    {
        this.SocketPath = socketPath;
        this.socket = socket;

        this.stream = new NetworkStream(socket, ownsSocket: false);
        var handler = new HeaderDelimitedMessageHandler(this.stream, this.stream, new JsonMessageFormatter());
        this.rpc = new JsonRpc(handler);
        this.rpc.StartListening();
        this.Proxy = this.rpc.Attach<IDalamudRpc>();
    }

    public static async Task<DalamudRpcClient> ConnectAsync(string socketPath, CancellationToken cancellationToken = default)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken).ConfigureAwait(false);

        return new DalamudRpcClient(socketPath, socket);
    }

    public async ValueTask DisposeAsync()
    {
        this.rpc.Dispose();
        await this.stream.DisposeAsync().ConfigureAwait(false);

        this.socket.Dispose();
    }
}
