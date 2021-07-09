using System;
using System.IO;
using System.Threading.Tasks;
using XIVLauncher.Game.Patch.PatchList;

namespace XIVLauncher.Game.Patch.Acquisition
{
    public interface IPatchAcquisition
    {
        public Task StartDownloadAsync(PatchListEntry patch, FileInfo outFile);
        public Task CancelAsync();

        public event EventHandler<AcquisitionProgress> ProgressChanged;
        public event EventHandler<AcquisitionResult> Complete;
    }
}
