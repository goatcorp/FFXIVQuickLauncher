using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Downloader;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.Windows;
using DownloadProgressChangedEventArgs = System.Net.DownloadProgressChangedEventArgs;

namespace XIVLauncher.Game.Patch
{
    public class PatchDownloadProgress
    {
        public long CurrentBytes { get; set; }
        public long Length {get;set;}
        public string Name {get;set;}
    }

    public enum PatchState
    {
        Nothing,
        IsDownloading,
        IsInstalling,
        Finished
    }

    public class PatchDownload
    {
        public PatchListEntry Patch { get; set; }
        public PatchState State { get; set; }
    }

    class PatchInstaller
    {
        private DownloadConfiguration _downloadOpt = new DownloadConfiguration
        {
            ParallelDownload = true, // download parts of file as parallel or not
            BufferBlockSize = 8000, // usually, hosts support max to 8000 bytes
            ChunkCount = 8, // file parts to download
            MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
            OnTheFlyDownload = false, // caching in-memory mode
            Timeout = 1000 // timeout (millisecond) per stream block reader
        };

        private const int MAX_DOWNLOADS_AT_ONCE = 4;

        private readonly string _uniqueId;
        private readonly string _repository;
        private readonly PatchDownloadDialog _progressDialog;

        private DirectoryInfo _patchDir;

        private volatile int _currentNumDownloads;
        private List<PatchDownload> _downloads;

        public PatchInstaller(string uniqueId, string repository, IEnumerable<PatchListEntry> patches, PatchDownloadDialog progressDialog)
        {
            _uniqueId = uniqueId;
            _repository = repository;
            _progressDialog = progressDialog;

            _patchDir = new DirectoryInfo(Path.Combine(Paths.XIVLauncherPath, "patches"));
            _patchDir.Create();

            _downloads = new List<PatchDownload>();
            foreach (var patchListEntry in patches)
            {
                _downloads.Add(new PatchDownload
                {
                    Patch = patchListEntry,
                    State = PatchState.Nothing
                });
            }
        }

        public void Start()
        {
            CheckDownloadQueue();
        }

        private async Task DownloadPatchAsync(PatchListEntry patch, int index)
        {
            var dlService = new DownloadService(_downloadOpt);
            dlService.DownloadProgressChanged += (sender, args) =>
            {
                Log.Verbose($"Downloading patch: {(int)Math.Round((double)(100 * args.BytesReceived) / patch.Length)}% ({args.BytesReceived} / {patch.Length}) - {patch.Url}");

                _progressDialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var pct = Math.Round((double) (100 * args.BytesReceived) / patch.Length);
                    _progressDialog.SetPatchProgress(index, $"{patch.VersionId} ({pct}%)", pct);
                }));
            };

            using (var client = new WebClient())
            {
                client.Headers.Add("user-agent", "FFXIV PATCH CLIENT");
                client.Headers.Add("X-Patch-Unique-Id", _uniqueId);

                var res = client.UploadString(
                    "http://patch-gamever.ffxiv.com/gen_token", patch.Url);

                //await dlService.DownloadFileAsync()

                Log.Verbose("Patch at {0} downloaded completely", patch.Url);
            }

            _currentNumDownloads--;
            CheckDownloadQueue();
            CheckApplyQueue();
        }

        private void CheckDownloadQueue()
        {
            while (_currentNumDownloads < MAX_DOWNLOADS_AT_ONCE)
            {
                var toDl = _downloads.FirstOrDefault(x => x.State == PatchState.Nothing);

                if (toDl == null)
                {
                    Log.Information("All patches downloaded.");
                    return;
                }

                _currentNumDownloads++;
                DownloadPatchAsync(toDl.Patch, _currentNumDownloads);
            }
        }

        private void CheckApplyQueue()
        {

        }
    }
}
