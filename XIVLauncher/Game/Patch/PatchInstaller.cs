using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Downloader;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.Settings;
using XIVLauncher.Windows;

namespace XIVLauncher.Game.Patch
{
    public enum PatchState
    {
        Nothing,
        IsDownloading,
        Downloaded,
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
            OnTheFlyDownload = true, // caching in-memory mode
            Timeout = 1000 // timeout (millisecond) per stream block reader
        };

        private const int MAX_DOWNLOADS_AT_ONCE = 4;

        private readonly string _uniqueId;
        private readonly Repository _repository;
        private readonly PatchDownloadDialog _progressDialog;
        private readonly DirectoryInfo _gamePath;

        private DirectoryInfo _patchDir;

        private int _currentNumDownloads;
        private List<PatchDownload> _downloads;

        private int _currentInstallIndex;

        private long[] _progresses = new long[MAX_DOWNLOADS_AT_ONCE];
        private long[] _speeeeeeds = new long[MAX_DOWNLOADS_AT_ONCE];

        private long AllDownloadsLength => _downloads.Where(x => x.State == PatchState.Nothing || x.State == PatchState.IsDownloading).Sum(x => x.Patch.Length) - _progresses.Sum();

        public PatchInstaller(string uniqueId, Repository repository, IEnumerable<PatchListEntry> patches, PatchDownloadDialog progressDialog, DirectoryInfo gamePath)
        {
            _uniqueId = uniqueId;
            _repository = repository;
            _progressDialog = progressDialog;
            _gamePath = gamePath;

            _patchDir = new DirectoryInfo(Path.Combine(Paths.XIVLauncherPath, "patches"));
            if (!_patchDir.Exists)
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
            Task.Run(RunDownloadQueue);
            Task.Run(RunApplyQueue);
        }

        private async Task DownloadPatchAsync(PatchDownload download, int index)
        {
            var outFile = GetPatchFile(download.Patch);

            if (outFile.Exists && IsHashCheckPass(download.Patch, outFile))
            {
                _currentNumDownloads--;
                download.State = PatchState.Downloaded;
                return;
            }

            var dlService = new DownloadService(_downloadOpt);
            dlService.DownloadProgressChanged += (sender, args) =>
            {
                _progressDialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _progresses[index] = args.BytesReceived;
                    _speeeeeeds[index] = dlService.DownloadSpeed;

                    var pct = Math.Round((double) (100 * args.BytesReceived) / download.Patch.Length, 2);
                    _progressDialog.SetPatchProgress(index, $"{download.Patch.VersionId} ({pct:#0.00}%, {Util.BytesToString(dlService.DownloadSpeed)}/s)", pct);
                }));
            };

            dlService.DownloadFileCompleted += (sender, args) =>
            {
                // Let's just bail for now, need better handling of this later
                if (!IsHashCheckPass(download.Patch, outFile))
                {
                    MessageBox.Show($"IsHashCheckPass FAILED for {download.Patch.VersionId}.\nPlease try again.", "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    outFile.Delete();
                    Environment.Exit(0);
                    return;
                }

                _currentNumDownloads--;
                download.State = PatchState.Downloaded;
            };

            using (var client = new WebClient())
            {
                client.Headers.Add("user-agent", "FFXIV PATCH CLIENT");
                client.Headers.Add("X-Patch-Unique-Id", _uniqueId);

                var res = client.UploadString(
                    "http://patch-gamever.ffxiv.com/gen_token", download.Patch.Url);

                await dlService.DownloadFileAsync(res, outFile.FullName);

                Log.Verbose("Patch at {0} downloaded completely", download.Patch.Url);
            }
        }

        private void RunDownloadQueue()
        {
            while (_downloads.Any(x => x.State == PatchState.Nothing))
            {
                Thread.Sleep(500);
                while (_currentNumDownloads < MAX_DOWNLOADS_AT_ONCE)
                {
                    var toDl = _downloads.FirstOrDefault(x => x.State == PatchState.Nothing);

                    if (toDl == null)
                    {
                        Log.Information("All patches downloaded.");
                        return;
                    }

                    toDl.State = PatchState.IsDownloading;
                    var a = _currentNumDownloads;
                    Task.Run(() => DownloadPatchAsync(toDl, a));
                    _currentNumDownloads++;
                    Debug.WriteLine("Started DL" + _currentNumDownloads);
                }

                _progressDialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _progressDialog.SetLeft(AllDownloadsLength, _speeeeeeds.Sum());
                }));
            }
        }

        private void RunApplyQueue()
        {
            while (_currentInstallIndex < _downloads.Count)
            {
                Thread.Sleep(500);

                var toInstall = _downloads[_currentInstallIndex];

                if (toInstall.State != PatchState.Downloaded)
                    continue;

                toInstall.State = PatchState.IsInstalling;
                MessageBox.Show("INSTALLING " + toInstall.Patch.VersionId);

                Thread.Sleep(10000); // waitin for winter

                _currentInstallIndex++;
            }

            Log.Information("PATCHING finish");

            // Overwrite the old BCK with the new game version
            _repository.GetVerFile(_gamePath).CopyTo(_repository.GetVerFile(_gamePath, true).FullName, true);
        }

        private bool IsHashCheckPass(PatchListEntry patchListEntry, FileInfo path)
        {
            var stream = path.OpenRead();

            var parts = (int) Math.Round((double) patchListEntry.Length / patchListEntry.HashBlockSize);
            var block = new byte[patchListEntry.HashBlockSize];

            for (var i = 0; i < parts; i++)
            {
                var read = stream.Read(block, 0, (int) patchListEntry.HashBlockSize);

                if (read < patchListEntry.HashBlockSize)
                {
                    var trimmedBlock = new byte[read];
                    Array.Copy(block, 0, trimmedBlock, 0, read);
                    block = trimmedBlock;
                }

                using (var sha1 = new SHA1Managed())
                {
                    var hash = sha1.ComputeHash(block);
                    var sb = new StringBuilder(hash.Length * 2); 

                    foreach (var b in hash)
                    {
                        sb.Append(b.ToString("x2"));
                    }

                    if (sb.ToString() == patchListEntry.Hashes[i])
                        continue;

                    stream.Close();
                    return false;
                }
            }

            stream.Close();
            return true;
        }

        private FileInfo GetPatchFile(PatchListEntry patch) =>
            new FileInfo(Path.Combine(_patchDir.FullName, $"{patch.VersionId}.zipatch"));
    }
}
