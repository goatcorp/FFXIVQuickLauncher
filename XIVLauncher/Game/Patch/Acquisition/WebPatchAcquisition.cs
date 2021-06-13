using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CheapLoc;
using Downloader;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.Windows;

namespace XIVLauncher.Game.Patch.Acquisition
{
    class WebPatchAcquisition : IPatchAcquisition
    {
        private readonly DirectoryInfo _patchStore;
        private DownloadService _dlService;

        private string DownloadTempPath => Path.Combine(_patchStore.FullName, "temp");

        private DownloadConfiguration _downloadOpt = new DownloadConfiguration
        {
            ParallelDownload = true, // download parts of file as parallel or not
            BufferBlockSize = 8000, // usually, hosts support max to 8000 bytes
            ChunkCount = 8, // file parts to download
            MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
            OnTheFlyDownload = false, // caching in-memory mode
            Timeout = 10000, // timeout (millisecond) per stream block reader
            TempDirectory = Path.GetTempPath(), // this is the library default
            RequestConfiguration = new RequestConfiguration
            {
                UserAgent = "FFXIV PATCH CLIENT",
                Accept = "*/*"
            },
            MaximumBytesPerSecond = App.Settings.SpeedLimitBytes / PatchManager.MAX_DOWNLOADS_AT_ONCE,
        };

        public WebPatchAcquisition(DirectoryInfo patchStore)
        {
            this._patchStore = patchStore;

            this._downloadOpt.TempDirectory = DownloadTempPath;
        }

        public async Task StartDownloadAsync(PatchListEntry patch, FileInfo outFile)
        {
            _dlService = new DownloadService(_downloadOpt);

            _dlService.DownloadProgressChanged += (sender, args) =>
            {
                ProgressChanged?.Invoke(null, new AcquisitionProgress
                {
                    BytesPerSecondSpeed = (long) args.BytesPerSecondSpeed,
                    Progress = args.ReceivedBytesSize
                });
            };

            _dlService.DownloadFileCompleted += (sender, args) =>
            {
                if (args.Error != null)
                {
                    Log.Error(args.Error, "[WEB] Download failed for {0} with reason {1}", patch.VersionId, args.Error);

                    // If we cancel downloads, we don't want to see an error message
                    if (args.Error is OperationCanceledException)
                    {
                        Complete?.Invoke(null, AcquisitionResult.Cancelled);
                        return;
                    }

                    Complete?.Invoke(null, AcquisitionResult.Error);
                    return;
                }

                if (args.Cancelled)
                {
                    Log.Error("[WEB] Download cancelled for {0} with reason {1}", patch.VersionId, args.Error);

                    /*
                    Cancellation should not produce an error message, since it is always triggered by another error or the user.
                    */
                    Complete?.Invoke(null, AcquisitionResult.Cancelled);
                    return;
                }

                Complete?.Invoke(null, AcquisitionResult.Success);
            };
            
            await _dlService.DownloadFileTaskAsync(patch.Url, outFile.FullName);
        }

        public async Task CancelAsync()
        {
            this._dlService.CancelAsync();
        }

        public event EventHandler<AcquisitionProgress> ProgressChanged;
        public event EventHandler<AcquisitionResult> Complete;
    }
}
