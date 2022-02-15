namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    public class PatcherIpcEnvelope
    {
        public PatcherIpcOpCode OpCode { get; set; }
        public object Data { get; set; }
    }
}
