namespace XIVLauncher.Common.PatcherIpc
{
    public class PatcherIpcEnvelope
    {
        public PatcherIpcOpCode OpCode { get; set; }
        public object Data { get; set; }
    }
}