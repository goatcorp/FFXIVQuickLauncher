using XIVLauncher.Common.Game.Patch;

namespace XIVLauncher.NativeAOT;

public class RepairProgress
{
    public string CurrentStep { get; private set; } = "";
    public string CurrentFile { get; private set; } = "";
    public long Total { get; private set; } = 100;
    public long Progress { get; private set; }
    public long Speed { get; private set; }

    public RepairProgress()
    {
    }

    public RepairProgress(PatchVerifier? verify)
    {
        if (verify is null)
            return;

        switch (verify.State)
        {
            case PatchVerifier.VerifyState.DownloadMeta:
                CurrentStep = "Downloading meta files...";
                CurrentFile = $"{verify.CurrentFile}";
                Total = verify.Total;
                Progress = verify.Progress;
                Speed = verify.Speed;
                break;

            case PatchVerifier.VerifyState.VerifyAndRepair:
                CurrentStep = verify.CurrentMetaInstallState switch
                {
                    Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.NotStarted => "Verifying game files...",
                    _ => "Repairing game files...",
                };

                CurrentFile = $"{verify.CurrentFile}";
                Total = verify.Total;
                Progress = verify.Progress;
                Speed = verify.Speed;
                break;

            case PatchVerifier.VerifyState.NotStarted:
            case PatchVerifier.VerifyState.Done:
            case PatchVerifier.VerifyState.Cancelled:
            case PatchVerifier.VerifyState.Error:
            default:
                return;
        }
    }
}