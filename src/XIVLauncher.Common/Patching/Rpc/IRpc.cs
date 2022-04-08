using System;
using XIVLauncher.Common.PatcherIpc;

namespace XIVLauncher.Common.Patching.Rpc;

public interface IRpc
{
    public void SendMessage(PatcherIpcEnvelope envelope);

    public event Action<PatcherIpcEnvelope> MessageReceived;
}
