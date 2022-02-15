namespace XIVLauncher.PatchInstaller.PartialFile.PartialPatchRpc
{
    public enum PartialPatchRpcOpcode
    {
        ProvideIndexFile,
        ProvideIndexFileFinish,
        RequestPartialFile,
        ProvidePartialFile,
        FinishPartialFile,
        StatusUpdate,
        Finished,
    }
}
