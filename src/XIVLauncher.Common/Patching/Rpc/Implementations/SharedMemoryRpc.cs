using System;
using System.Text;
using Newtonsoft.Json;
using Serilog;
using SharedMemory;
using XIVLauncher.Common.PatcherIpc;

namespace XIVLauncher.Common.Patching.Rpc.Implementations;

public class SharedMemoryRpc : IRpc, IDisposable
{
    private readonly RpcBuffer rpcBuffer;

    public SharedMemoryRpc(string channelName)
    {
        this.rpcBuffer = new RpcBuffer(channelName, RemoteCallHandler);
    }

    private void RemoteCallHandler(ulong msgId, byte[] payload)
    {
        var json = IpcHelpers.Base64Decode(Encoding.ASCII.GetString(payload));
        Log.Information("[SHMEMRPC] IPC({0}): {1}", msgId, json);

        var msg = JsonConvert.DeserializeObject<PatcherIpcEnvelope>(json, IpcHelpers.JsonSettings);
        MessageReceived?.Invoke(msg);
    }

    public void SendMessage(PatcherIpcEnvelope envelope)
    {
        var json = IpcHelpers.Base64Encode(JsonConvert.SerializeObject(envelope, IpcHelpers.JsonSettings));
        this.rpcBuffer.RemoteRequest(Encoding.ASCII.GetBytes(json));
    }

    public event Action<PatcherIpcEnvelope> MessageReceived;

    public void Dispose()
    {
        rpcBuffer?.Dispose();
    }
}
