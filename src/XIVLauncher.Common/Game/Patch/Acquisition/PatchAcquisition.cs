using System;
using System.IO;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Game.Patch.Acquisition
{
    public abstract class PatchAcquisition
    {
        public abstract Task StartDownloadAsync(string url, FileInfo outFile);
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
