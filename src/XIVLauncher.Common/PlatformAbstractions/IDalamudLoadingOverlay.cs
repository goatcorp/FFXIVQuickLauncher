namespace XIVLauncher.Common.PlatformAbstractions;

public interface IDalamudLoadingOverlay
{
    public enum DalamudUpdateStep
    {
        Dalamud,
        Assets,
        Runtime,
        Unavailable,
        Starting,
    }

    public void SetStep(DalamudUpdateStep step);

    public void SetVisible();

    public void SetInvisible();

    public void ReportProgress(long? size, long downloaded, double? progress);
}