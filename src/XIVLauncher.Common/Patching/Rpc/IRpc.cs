using System;
using XIVLauncher.Common.PatcherIpc;

namespace XIVLauncher.Common.Patching.Rpc;

public interface IRpc
{
    public bool SendMessage(PatcherIpcEnvelope envelope);

    public event Action<PatcherIpcEnvelope> MessageReceived;
}
