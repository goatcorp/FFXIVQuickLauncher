using System;
using System.Collections.Generic;
using XIVLauncher.Common.PatcherIpc;

namespace XIVLauncher.Common.Patching.Rpc.Implementations;

public class InProcessRpc : IRpc, IDisposable
{
    private readonly string channelName;

    private static readonly Dictionary<string, List<InProcessRpc>> instanceMapping = new();

    public InProcessRpc(string channelName)
    {
        this.channelName = channelName;

        if (!instanceMapping.TryGetValue(channelName, out var instanceList))
        {
            instanceList = new List<InProcessRpc>();
            instanceMapping.Add(channelName, instanceList);
        }

        instanceList.Add(this);
    }

    public void SendMessage(PatcherIpcEnvelope envelope)
    {
        var list = instanceMapping[this.channelName];

        foreach (InProcessRpc otherInstance in list)
        {
            if (otherInstance == this)
                return;

            otherInstance.Dispatch(envelope);
        }
    }

    private void Dispatch(PatcherIpcEnvelope envelope)
    {
        this.MessageReceived?.Invoke(envelope);
    }

    public event Action<PatcherIpcEnvelope> MessageReceived;

    public void Dispose()
    {
        instanceMapping[this.channelName].Remove(this);
    }
}
