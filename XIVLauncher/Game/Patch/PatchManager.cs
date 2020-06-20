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

    class PatchManager
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
            }
        };

        public event EventHandler OnFinish;

        private const int MAX_DOWNLOADS_AT_ONCE = 4;

        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

        private readonly Repository _repository;
        private readonly PatchDownloadDialog _progressDialog;
        private readonly DirectoryInfo _gamePath;
        private readonly DirectoryInfo _patchStore;
        private readonly PatchInstaller _installer;

        private List<PatchDownload> _downloads;

        private int _currentInstallIndex;

        private long[] _progresses = new long[MAX_DOWNLOADS_AT_ONCE];
        private long[] _speeeeeeds = new long[MAX_DOWNLOADS_AT_ONCE];
        private bool[] _slots = new bool[MAX_DOWNLOADS_AT_ONCE];

        private long AllDownloadsLength => _downloads.Where(x => x.State == PatchState.Nothing || x.State == PatchState.IsDownloading).Sum(x => x.Patch.Length) - _progresses.Sum();

        public PatchManager(Repository repository, IEnumerable<PatchListEntry> patches, PatchDownloadDialog progressDialog, DirectoryInfo gamePath, DirectoryInfo patchStore, PatchInstaller installer)
        {
            _repository = repository;
            _progressDialog = progressDialog;
            _gamePath = gamePath;
            _patchStore = patchStore;
            _installer = installer;

            if (!_patchStore.Exists)
                _patchStore.Create();

            _downloads = new List<PatchDownload>();
            foreach (var patchListEntry in patches)
            {
                _downloads.Add(new PatchDownload
                {
                    Patch = patchListEntry,
                    State = PatchState.Nothing
                });
            }

            // All dl slots are available at the start
            for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
            {
                _slots[i] = true;
            }

            ServicePointManager.DefaultConnectionLimit = 255;
        }

        public void Start()
        {
#if !DEBUG
            if ((long) Util.GetDiskFreeSpace(_patchStore.FullName) < AllDownloadsLength)
            {
                OnFinish?.Invoke(this, null);

                MessageBox.Show(
                    "There is not enough space on your drive to download and install patches.\n\nYou can change the location patches are downloaded to in the settings.", "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
#endif

            _installer.StartIfNeeded();

            Task.Run(RunDownloadQueue, _cancelTokenSource.Token);
            Task.Run(RunApplyQueue, _cancelTokenSource.Token);
        }

        private async Task DownloadPatchAsync(PatchDownload download, int index)
        {
            var outFile = GetPatchFile(download.Patch);

            _progressDialog.Dispatcher.BeginInvoke(new Action(() =>
            {
                _progressDialog.SetPatchProgress(index, $"{download.Patch.VersionId} ({_progressDialog.ViewModel.PatchCheckingLoc})", 0f);
            }));

            if (outFile.Exists && IsHashCheckPass(download.Patch, outFile))
            {
                download.State = PatchState.Downloaded;
                _slots[index] = true;
                _progresses[index] = download.Patch.Length;

                _progressDialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _progressDialog.SetPatchProgress(index, _progressDialog.ViewModel.PatchDoneLoc, 100f);
                }));
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

                _progressDialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _progressDialog.SetPatchProgress(index, _progressDialog.ViewModel.PatchDoneLoc, 100f);
                }));

                download.State = PatchState.Downloaded;
                _slots[index] = true;
            };

            await dlService.DownloadFileAsync(download.Patch.Url, outFile.FullName);

            Log.Verbose("Patch at {0} downloaded completely", download.Patch.Url);
        }

        private void RunDownloadQueue()
        {
            while (_downloads.Any(x => x.State == PatchState.Nothing))
            {
                Thread.Sleep(500);
                for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
                {
                    if (!_slots[i]) 
                        continue;

                    _slots[i] = false;

                    var toDl = _downloads.FirstOrDefault(x => x.State == PatchState.Nothing);

                    if (toDl == null)
                    {
                        Log.Information("All patches downloaded.");
                        return;
                    }

                    toDl.State = PatchState.IsDownloading;
                    var curIndex = i;
                    Task.Run(() => DownloadPatchAsync(toDl, curIndex));
                    Debug.WriteLine("Started DL" + i);
                }

                _progressDialog.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _progressDialog.SetLeft(AllDownloadsLength, _speeeeeeds.Sum());
                }));
            }
        }

        private void RunApplyQueue()
        {
            _progressDialog.Dispatcher.Invoke(() =>
                _progressDialog.SetGeneralProgress(0,
                    _downloads.Count));

            while (_currentInstallIndex < _downloads.Count)
            {
                Thread.Sleep(500);

                var toInstall = _downloads[_currentInstallIndex];

                if (toInstall.State != PatchState.Downloaded)
                    continue;

                toInstall.State = PatchState.IsInstalling;
                MessageBox.Show("INSTALLING " + toInstall.Patch.VersionId);

                _installer.StartInstall(_gamePath, GetPatchFile(toInstall.Patch), _repository);

                while (_installer.State != PatchInstaller.InstallerState.Ready)
                {
                    Thread.Sleep(100);
                }

                // TODO need to handle this better
                if (_installer.State == PatchInstaller.InstallerState.Failed)
                    return;

                File.WriteAllText(GetRepoForPatch(toInstall.Patch).GetVerFile(_gamePath).FullName, toInstall.Patch.VersionId);

                toInstall.State = PatchState.Finished;
                _currentInstallIndex++;

                _progressDialog.Dispatcher.Invoke(() =>
                    _progressDialog.SetGeneralProgress(_currentInstallIndex,
                        _downloads.Count));
            }

            Log.Information("PATCHING finish");

            VerToBck();
            OnFinish?.Invoke(this, null);
        }

        private static bool IsHashCheckPass(PatchListEntry patchListEntry, FileInfo path)
        {
            if (patchListEntry.HashType != "sha1")
            {
                Log.Error("??? Unknown HashType: {0} for {1}", patchListEntry.HashType, patchListEntry.Url);
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

        private void VerToBck()
        {
            foreach (var repository in Enum.GetValues(typeof(Repository)).Cast<Repository>())
            {
                // Overwrite the old BCK with the new game version
                _repository.GetVerFile(_gamePath).CopyTo(_repository.GetVerFile(_gamePath, true).FullName, true);
            }
        }

        private const string BASE_GAME_VERSION = "2012.01.01.0000.0000";
        public static void SetupGameBase(DirectoryInfo gameDirectory)
        {
            if (!gameDirectory.Exists)
                gameDirectory.Create();

            var boot = gameDirectory.CreateSubdirectory("boot");
            File.WriteAllText(Path.Combine(boot.FullName, "ffxivboot.ver"), BASE_GAME_VERSION);
            File.WriteAllText(Path.Combine(boot.FullName, "ffxivboot.bck"), BASE_GAME_VERSION);

            var game = gameDirectory.CreateSubdirectory("game");
            File.WriteAllText(Path.Combine(game.FullName, "ffxivgame.ver"), BASE_GAME_VERSION);
            File.WriteAllText(Path.Combine(game.FullName, "ffxivgame.bck"), BASE_GAME_VERSION);

            var sqPack = game.CreateSubdirectory("sqpack");

            var ex1 = sqPack.CreateSubdirectory("ex1");
            File.WriteAllText(Path.Combine(ex1.FullName, "ex1.ver"), BASE_GAME_VERSION);
            File.WriteAllText(Path.Combine(ex1.FullName, "ex1.bck"), BASE_GAME_VERSION);

            var ex2 = sqPack.CreateSubdirectory("ex2");
            File.WriteAllText(Path.Combine(ex2.FullName, "ex2.ver"), BASE_GAME_VERSION);
            File.WriteAllText(Path.Combine(ex2.FullName, "ex2.bck"), BASE_GAME_VERSION);

            var ex3 = sqPack.CreateSubdirectory("ex3");
            File.WriteAllText(Path.Combine(ex3.FullName, "ex3.ver"), BASE_GAME_VERSION);
            File.WriteAllText(Path.Combine(ex3.FullName, "ex3.bck"), BASE_GAME_VERSION);
        }
    }
}
