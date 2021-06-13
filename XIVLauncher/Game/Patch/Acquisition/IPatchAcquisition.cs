using System;
using System.IO;
using System.Threading.Tasks;
using XIVLauncher.Game.Patch.PatchList;

namespace XIVLauncher.Game.Patch.Acquisition
{
    interface IPatchAcquisition
    {
        public Task StartDownloadAsync(PatchListEntry patch, DirectoryInfo patchStore);

        public event EventHandler<AcquisitionProgress> ProgressChanged;
        public event EventHandler Complete;
    }
}
