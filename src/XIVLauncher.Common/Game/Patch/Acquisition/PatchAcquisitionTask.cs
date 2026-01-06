using System;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Game.Patch.Acquisition
{
    public abstract class PatchAcquisitionTask
    {
        public abstract Task StartAsync();
        public abstract Task CancelAsync();

        public event EventHandler<AcquisitionProgress> ProgressChanged;

        protected void OnProgressChanged(AcquisitionProgress progress)
        {
            this.ProgressChanged?.Invoke(this, progress);
        }

        public event EventHandler<AcquisitionResult> Complete;

        protected void OnComplete(AcquisitionResult result)
        {
            this.Complete?.Invoke(this, result);
        }
    }
}
