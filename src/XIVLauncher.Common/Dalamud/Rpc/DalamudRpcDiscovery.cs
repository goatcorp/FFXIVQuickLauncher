using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Dalamud.Rpc.Types;

namespace XIVLauncher.Common.Dalamud.Rpc;

public class DalamudRpcDiscovery(string? searchPath = null, int connectionTimeoutMs = 100)
{
    private readonly string searchPath = searchPath ?? Path.Combine(Path.GetTempPath(), "XIVLauncher");

    public async IAsyncEnumerable<DiscoveredClient> SearchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(this.searchPath))
        {
            yield break;
        }

        // Find candidate socket files using known pattern
        var candidates = Directory.GetFiles(this.searchPath, "DalamudRPC.*.sock", SearchOption.TopDirectoryOnly);

        foreach (var socketPath in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Log.Information("Found candidate socket: {SocketPath}", socketPath);
            var discoveredClient = await this.TryConnectClientAsync(socketPath, cancellationToken).ConfigureAwait(false);

            if (discoveredClient != null)
            {
                yield return discoveredClient;
            }
        }
    }

    private async Task<DiscoveredClient?> TryConnectClientAsync(string socketPath, CancellationToken cancellationToken)
    {
        DalamudRpcClient? client = null;

        try
        {
            client = await DalamudRpcClient.ConnectAsync(socketPath, cancellationToken).ConfigureAwait(false);

            var helloRequest = new ClientHelloRequest();
            var response = await HelloWithTimeoutAsync(client.Proxy, helloRequest, connectionTimeoutMs, cancellationToken).ConfigureAwait(false);
            Log.Debug("Received hello response from socket {SocketPath}: {@Response}", socketPath, response);

            if (!string.IsNullOrEmpty(response.ClientState))
            {
                var discoveredClient = new DiscoveredClient(response, client);
                client = null;

                return discoveredClient;
            }

            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to attach to socket: {SocketPath}", socketPath);
            return null;
        }
        finally
        {
            if (client != null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<ClientHelloResponse> HelloWithTimeoutAsync(
        IDalamudRpc rpcProxy,
        ClientHelloRequest request,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            return await rpcProxy.HelloAsync(request).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Hello request timed out after {timeoutMs}ms");
        }
    }
}

public record DiscoveredClient(ClientHelloResponse HelloResponse, DalamudRpcClient Client) : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        await this.Client.DisposeAsync().ConfigureAwait(false);
    }
}
