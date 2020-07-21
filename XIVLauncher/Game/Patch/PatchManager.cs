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
using CheapLoc;
using Downloader;
using Serilog;
using XIVLauncher.Game.Patch.PatchList;
using XIVLauncher.PatchInstaller;
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

    public class PatchManager
    {
        private DownloadConfiguration _downloadOpt = new DownloadConfiguration
        {
            ParallelDownload = true, // download parts of file as parallel or not
            BufferBlockSize = 8000, // usually, hosts support max to 8000 bytes
            ChunkCount = 8, // file parts to download
            MaxTryAgainOnFailover = int.MaxValue, // the maximum number of times to fail.
            OnTheFlyDownload = false, // caching in-memory mode
            Timeout = 1000, // timeout (millisecond) per stream block reader
            RequestConfiguration = new RequestConfiguration
            {
                UserAgent = "FFXIV PATCH CLIENT",
                Accept = "*/*"
            },
            MaximumBytesPerSecond = App.Settings.SpeedLimitBytes / MAX_DOWNLOADS_AT_ONCE
        };

        public event EventHandler<bool> OnFinish;

        public const int MAX_DOWNLOADS_AT_ONCE = 4;

        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

        private readonly Repository _repository;
        private readonly DirectoryInfo _gamePath;
        private readonly DirectoryInfo _patchStore;
        private readonly PatchInstaller _installer;

        public readonly IReadOnlyList<PatchDownload> Downloads;

        public int CurrentInstallIndex { get; private set; }

        public readonly long[] Progresses = new long[MAX_DOWNLOADS_AT_ONCE];
        public readonly long[] Speeds = new long[MAX_DOWNLOADS_AT_ONCE];
        public readonly PatchDownload[] Actives = new PatchDownload[MAX_DOWNLOADS_AT_ONCE];
        public readonly bool[] Slots = new bool[MAX_DOWNLOADS_AT_ONCE];

        public bool DownloadsDone { get; private set; }

        public long AllDownloadsLength => Downloads.Where(x => x.State == PatchState.Nothing || x.State == PatchState.IsDownloading).Sum(x => x.Patch.Length) - Progresses.Sum();

        public PatchManager(Repository repository, IEnumerable<PatchListEntry> patches, DirectoryInfo gamePath, DirectoryInfo patchStore, PatchInstaller installer)
        {
            _repository = repository;
            _gamePath = gamePath;
            _patchStore = patchStore;
            _installer = installer;

            if (!_patchStore.Exists)
                _patchStore.Create();

            Downloads = patches.Select(patchListEntry => new PatchDownload {Patch = patchListEntry, State = PatchState.Nothing}).ToList().AsReadOnly();

            // All dl slots are available at the start
            for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
            {
                Slots[i] = true;
            }

            ServicePointManager.DefaultConnectionLimit = 255;
        }

        public void Start()
        {
#if !DEBUG
            var freeSpaceDownload = (long)Util.GetDiskFreeSpace(_patchStore.Root.FullName);

            if (freeSpaceDownload < AllDownloadsLength)
            {
                OnFinish?.Invoke(this, false);

                MessageBox.Show(string.Format(Loc.Localize("FreeSpaceError", "There is not enough space on your drive to download patches.\n\nYou can change the location patches are downloaded to in the settings.\n\nRequired:{0}\nFree:{1}"), Util.BytesToString(AllDownloadsLength), Util.BytesToString(freeSpaceDownload)), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var freeSpaceGame = (long)Util.GetDiskFreeSpace(_gamePath.Root.FullName);

            if (freeSpaceGame < AllDownloadsLength)
            {
                OnFinish?.Invoke(this, false);

                MessageBox.Show(string.Format(Loc.Localize("FreeSpaceGameError", "There is not enough space on your drive to install patches.\n\nYou can change the location the game is installed to in the settings.\n\nRequired:{0}\nFree:{1}"), Util.BytesToString(AllDownloadsLength), Util.BytesToString(freeSpaceGame)), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
#endif

            _installer.StartIfNeeded();
            _installer.WaitOnHello();

            Task.Run(RunDownloadQueue, _cancelTokenSource.Token);
            Task.Run(RunApplyQueue, _cancelTokenSource.Token);
        }

        private async Task DownloadPatchAsync(PatchDownload download, int index)
        {
            var outFile = GetPatchFile(download.Patch);

            Log.Information("Downloading patch {0} at {1} to {2}", download.Patch.VersionId, download.Patch.Url, outFile.FullName);

            Actives[index] = download;

            if (outFile.Exists && IsHashCheckPass(download.Patch, outFile))
            {
                download.State = PatchState.Downloaded;
                Slots[index] = true;
                Progresses[index] = download.Patch.Length;
                return;
            }

            var dlService = new DownloadService(_downloadOpt);
            dlService.DownloadProgressChanged += (sender, args) =>
            {
                Progresses[index] = args.BytesReceived;
                Speeds[index] = dlService.DownloadSpeed;
            };

            dlService.DownloadFileCompleted += (sender, args) =>
            {
                if (args.Error != null)
                {
                    Log.Error(args.Error, "Download failed for {0}", download.Patch.VersionId);
                    MessageBox.Show($"Download FAILED for {download.Patch.VersionId}.\nPlease try again.\n\n" + args.Error, "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                    return;
                }

                if (args.Cancelled)
                {
                    Log.Error("Download cancelled for {0}", download.Patch.VersionId);
                    MessageBox.Show($"Download CANCELLED for {download.Patch.VersionId}.\nPlease try again.\n\n" + args.Error, "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                }

                // Let's just bail for now, need better handling of this later
                if (!IsHashCheckPass(download.Patch, outFile))
                {
                    Log.Error("HashCHeck failed for {0} after DL", download.Patch.VersionId);
                    MessageBox.Show($"IsHashCheckPass FAILED for {download.Patch.VersionId}.\nPlease try again.", "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    outFile.Delete();
                    Environment.Exit(0);
                    return;
                }

                download.State = PatchState.Downloaded;
                Slots[index] = true;

                Log.Verbose("Patch at {0} downloaded completely", download.Patch.Url);
            };

            await dlService.DownloadFileAsync(download.Patch.Url, outFile.FullName);
        }

        private void RunDownloadQueue()
        {
            while (Downloads.Any(x => x.State == PatchState.Nothing))
            {
                Thread.Sleep(500);
                for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
                {
                    if (!Slots[i]) 
                        continue;

                    Slots[i] = false;

                    var toDl = Downloads.FirstOrDefault(x => x.State == PatchState.Nothing);

                    if (toDl == null)
                    {
                        Log.Information("All patches downloaded.");

                        DownloadsDone = true;
                        return;
                    }

                    toDl.State = PatchState.IsDownloading;
                    var curIndex = i;
                    Task.Run(() => DownloadPatchAsync(toDl, curIndex));
                }
            }
        }

        private void RunApplyQueue()
        {
            while (CurrentInstallIndex < Downloads.Count)
            {
                Thread.Sleep(500);

                var toInstall = Downloads[CurrentInstallIndex];

                if (toInstall.State != PatchState.Downloaded)
                    continue;

                toInstall.State = PatchState.IsInstalling;

#if DEBUG
                MessageBox.Show("INSTALLING " + toInstall.Patch.VersionId);
#endif

                Log.Information("Starting patch install for {0} at {1}", toInstall.Patch.VersionId, toInstall.Patch.Url);

                _installer.StartInstall(_gamePath, GetPatchFile(toInstall.Patch), toInstall.Patch, GetRepoForPatch(toInstall.Patch));

                while (_installer.State != PatchInstaller.InstallerState.Ready)
                {
                    Thread.Yield();
                }

                // TODO need to handle this better
                if (_installer.State == PatchInstaller.InstallerState.Failed)
                    return;

                toInstall.State = PatchState.Finished;
                CurrentInstallIndex++;
            }

            Log.Information("PATCHING finish");
            _installer.FinishInstall(_gamePath);

            OnFinish?.Invoke(this, true);
        }

        private static bool IsHashCheckPass(PatchListEntry patchListEntry, FileInfo path)
        {
            if (patchListEntry.HashType != "sha1")
            {
                Log.Verbose("??? Unknown HashType: {0} for {1}", patchListEntry.HashType, patchListEntry.Url);
                return true;
            }

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

                using var sha1 = new SHA1Managed();

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

            stream.Close();
            return true;
        }

        private FileInfo GetPatchFile(PatchListEntry patch)
        {
            var file = new FileInfo(Path.Combine(_patchStore.FullName, patch.Url.Substring("http://patch-dl.ffxiv.com/".Length)));
            file.Directory.Create();

            return file;
        }

        private Repository GetRepoForPatch(PatchListEntry patch)
        {
            if (patch.Url.Contains("boot"))
                return Repository.Boot;

            if (patch.Url.Contains("ex1"))
                return Repository.Ex1;

            if (patch.Url.Contains("ex2"))
                return Repository.Ex2;

            if (patch.Url.Contains("ex3"))
                return Repository.Ex3;

            return Repository.Ffxiv;
        }
    }
}
