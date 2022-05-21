using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Core.Components.LoadingPage;

public class DalamudOverlayInfoProxy : IDalamudLoadingOverlay
{
    public bool IsVisible { get; private set; }

    public IDalamudLoadingOverlay.DalamudUpdateStep Step { get; private set; }

    public void SetStep(IDalamudLoadingOverlay.DalamudUpdateStep step)
    {
        this.Step = step;
    }

    public void SetVisible()
    {
        this.IsVisible = true;
    }

    public void SetInvisible()
    {
        this.IsVisible = true;
    }

    public void ReportProgress(long? size, long downloaded, double? progress)
    {
        // TODO
    }
}