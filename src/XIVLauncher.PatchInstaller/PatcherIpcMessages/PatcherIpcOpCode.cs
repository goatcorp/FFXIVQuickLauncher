namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    public enum PatcherIpcOpCode
    {
        Hello,
        Bye,
        StartInstall,
        InstallRunning,
        InstallOk,
        InstallFailed,
        Finish
    }
}
