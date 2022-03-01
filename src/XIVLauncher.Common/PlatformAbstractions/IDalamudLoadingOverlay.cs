namespace XIVLauncher.Common.PlatformAbstractions;

public interface IDalamudLoadingOverlay
{
    public enum DalamudUpdateStep
    {
        Dalamud,
        Assets,
        Runtime,
        Unavailable
    }

    public void SetStep(DalamudUpdateStep step);

    public void SetVisible();

    public void SetInvisible();
}