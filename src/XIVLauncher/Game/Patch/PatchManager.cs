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
using XIVLauncher.Game.Patch.Acquisition;
using XIVLauncher.Game.Patch.Acquisition.Aria;
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
        public const int MAX_DOWNLOADS_AT_ONCE = 4;

        private readonly CancellationTokenSource _cancelTokenSource = new();

        private readonly DirectoryInfo _gamePath;
        private readonly DirectoryInfo _patchStore;
        private readonly PatchInstaller _installer;

        public readonly IReadOnlyList<PatchDownload> Downloads;

        public bool IsDone { get; private set; }

        public bool IsSuccess { get; private set; }

        public int CurrentInstallIndex { get; private set; }

        public enum SlotState
        {
            InProgress,
            Checking,
            Done,
        }

        public readonly long[] Progresses = new long[MAX_DOWNLOADS_AT_ONCE];
        public readonly double[] Speeds = new double[MAX_DOWNLOADS_AT_ONCE];
        public readonly PatchDownload[] Actives = new PatchDownload[MAX_DOWNLOADS_AT_ONCE];
        public readonly SlotState[] Slots = new SlotState[MAX_DOWNLOADS_AT_ONCE];
        public readonly PatchAcquisition[] DownloadServices = new PatchAcquisition[MAX_DOWNLOADS_AT_ONCE];

        public bool IsInstallerBusy { get; private set; }

        public bool DownloadsDone { get; private set; }

        public long AllDownloadsLength => GetDownloadLength();

        public PatchManager(IEnumerable<PatchListEntry> patches, DirectoryInfo gamePath, DirectoryInfo patchStore, PatchInstaller installer)
        {
            Debug.Assert(patches != null, "patches != null ASSERTION FAILED");

            _gamePath = gamePath;
            _patchStore = patchStore;
            _installer = installer;

            if (!_patchStore.Exists)
                _patchStore.Create();

            Downloads = patches.Select(patchListEntry => new PatchDownload {Patch = patchListEntry, State = PatchState.Nothing}).ToList().AsReadOnly();

            // All dl slots are available at the start
            for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
            {
                Slots[i] = SlotState.Done;
            }
        }

        public void Start()
        {
#if !DEBUG
            var freeSpaceDownload = (long)Util.GetDiskFreeSpace(_patchStore.Root.FullName);

            if (Downloads.Any(x => x.Patch.Length > freeSpaceDownload))
            {
                IsSuccess = false;
                IsDone = true;

                MessageBox.Show(string.Format(Loc.Localize("FreeSpaceError", "There is not enough space on your drive to download patches.\n\nYou can change the location patches are downloaded to in the settings.\n\nRequired:{0}\nFree:{1}"), Util.BytesToString(Downloads.OrderByDescending(x => x.Patch.Length).First().Patch.Length), Util.BytesToString(freeSpaceDownload)), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // If the first 6 patches altogether are bigger than the patch drive, we might run out of space
            if (freeSpaceDownload < GetDownloadLength(6))
            {
                IsSuccess = false;
                IsDone = true;

                MessageBox.Show(string.Format(Loc.Localize("FreeSpaceErrorAll", "There is not enough space on your drive to download all patches.\n\nYou can change the location patches are downloaded to in the XIVLauncher settings.\n\nRequired:{0}\nFree:{1}"), Util.BytesToString(AllDownloadsLength), Util.BytesToString(freeSpaceDownload)), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var freeSpaceGame = (long)Util.GetDiskFreeSpace(_gamePath.Root.FullName);

            if (freeSpaceGame < AllDownloadsLength)
            {
                IsSuccess = false;
                IsDone = true;

                MessageBox.Show(string.Format(Loc.Localize("FreeSpaceGameError", "There is not enough space on your drive to install patches.\n\nYou can change the location the game is installed to in the settings.\n\nRequired:{0}\nFree:{1}"), Util.BytesToString(AllDownloadsLength), Util.BytesToString(freeSpaceGame)), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
#endif

            if(!_installer.StartIfNeeded())
            {
                CustomMessageBox.Show(Loc.Localize("PatchManNoInstaller", "The patch installer could not start correctly.\n\nIf you have denied access to it, please try again. If this issue persists, please contact us via Discord."), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                IsSuccess = false;
                IsDone = true;
                return;
            }
            _installer.WaitOnHello();

            this.InitializeAcquisition().GetAwaiter().GetResult();

            Task.Run(RunDownloadQueue, _cancelTokenSource.Token);
            Task.Run(RunApplyQueue, _cancelTokenSource.Token);
        }

        public async Task InitializeAcquisition()
        {
            // TODO: Come up with a better pattern for initialization. This sucks.
            switch (App.Settings.PatchAcquisitionMethod.GetValueOrDefault(AcquisitionMethod.Aria))
            {
                case AcquisitionMethod.NetDownloader:
                    // ignored
                    break;
                case AcquisitionMethod.MonoTorrentNetFallback:
                    await TorrentPatchAcquisition.InitializeAsync(App.Settings.SpeedLimitBytes / MAX_DOWNLOADS_AT_ONCE);
                    break;
                case AcquisitionMethod.MonoTorrentAriaFallback:
                    await AriaHttpPatchAcquisition.InitializeAsync(App.Settings.SpeedLimitBytes / MAX_DOWNLOADS_AT_ONCE);
                    await TorrentPatchAcquisition.InitializeAsync(App.Settings.SpeedLimitBytes / MAX_DOWNLOADS_AT_ONCE);
                    break;
                case AcquisitionMethod.Aria:
                    await AriaHttpPatchAcquisition.InitializeAsync(App.Settings.SpeedLimitBytes / MAX_DOWNLOADS_AT_ONCE);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static async Task UnInitializeAcquisition()
        {
            try
            {
                await AriaHttpPatchAcquisition.UnInitializeAsync();
                await TorrentPatchAcquisition.UnInitializeAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not uninitialize patch acquisition.");
            }
        }

        private async Task DownloadPatchAsync(PatchDownload download, int index)
        {
            var outFile = GetPatchFile(download.Patch);

            Log.Information("Downloading patch {0} at {1} to {2}", download.Patch.VersionId, download.Patch.Url, outFile.FullName);

            Actives[index] = download;

            if (outFile.Exists && CheckPatchValidity(download.Patch, outFile) == HashCheckResult.Pass)
            {
                download.State = PatchState.Downloaded;
                Slots[index] = SlotState.Done;
                Progresses[index] = download.Patch.Length;
                return;
            }

            PatchAcquisition acquisition;

            switch (App.Settings.PatchAcquisitionMethod.GetValueOrDefault(AcquisitionMethod.Aria))
            {
                case AcquisitionMethod.NetDownloader:
                    acquisition = new NetDownloaderPatchAcquisition(this._patchStore);
                    break;
                case AcquisitionMethod.MonoTorrentNetFallback:
                    acquisition = new TorrentPatchAcquisition();

                    var torrentAcquisition = acquisition as TorrentPatchAcquisition;
                    if (!torrentAcquisition.IsApplicable(download.Patch))
                        acquisition = new NetDownloaderPatchAcquisition(this._patchStore);
                    break;
                case AcquisitionMethod.MonoTorrentAriaFallback:
                    acquisition = new TorrentPatchAcquisition();

                    torrentAcquisition = acquisition as TorrentPatchAcquisition;
                    if (!torrentAcquisition.IsApplicable(download.Patch))
                        acquisition = new AriaHttpPatchAcquisition();
                    break;
                case AcquisitionMethod.Aria:
                    acquisition = new AriaHttpPatchAcquisition();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            acquisition.ProgressChanged += (sender, args) =>
            {
                Progresses[index] = args.Progress;
                Speeds[index] = args.BytesPerSecondSpeed;
            };

            acquisition.Complete += (sender, args) =>
            {
                if (args == AcquisitionResult.Error)
                {
                    Log.Error("Download failed for {0}", download.Patch.VersionId);

                    CancelAllDownloads();
                    CustomMessageBox.Show(string.Format(Loc.Localize("PatchManDlFailure", "XIVLauncher could not verify the downloaded game files.\n\nThis usually indicates a problem with your internet connection.\nIf this error occurs again, try using a VPN set to Japan.\n\nContext: {0}\n{1}"), "Problem", download.Patch.VersionId), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                    return;
                }

                if (args == AcquisitionResult.Cancelled)
                {
                    Log.Error("Download cancelled for {0}", download.Patch.VersionId);
                    /*
                    Cancellation should not produce an error message, since it is always triggered by another error or the user.

                    CancelAllDownloads();
                    CustomMessageBox.Show(string.Format(Loc.Localize("PatchManDlFailure", "XIVLauncher could not verify the downloaded game files.\n\nThis usually indicates a problem with your internet connection.\nIf this error occurs again, try using a VPN set to Japan.\n\nContext: {0}\n{1}"), "Cancelled", download.Patch.VersionId), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                    */
                    return;
                }

                // Indicate "Checking..."
                Slots[index] = SlotState.Checking;

                var checkResult = CheckPatchValidity(download.Patch, outFile);

                // Let's just bail for now, need better handling of this later
                if (checkResult != HashCheckResult.Pass)
                {
                    CancelAllDownloads();
                    Log.Error("IsHashCheckPass failed for {0} after DL", download.Patch.VersionId);
                    CustomMessageBox.Show(string.Format(Loc.Localize("PatchManDlFailure", "XIVLauncher could not verify the downloaded game files.\n\nThis usually indicates a problem with your internet connection.\nIf this error occurs again, try using a VPN set to Japan.\n\nContext: {0}\n{1}"), $"IsHashCheckPass({checkResult})", download.Patch.VersionId), "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    outFile.Delete();
                    Environment.Exit(0);
                    return;
                }

                download.State = PatchState.Downloaded;
                Slots[index] = SlotState.Done;
                Progresses[index] = 0;
                Speeds[index] = 0;

                Log.Information("Patch at {0} downloaded completely", download.Patch.Url);

                this.CheckIsDone();
            };

            DownloadServices[index] = acquisition;

            await acquisition.StartDownloadAsync(download.Patch, outFile);
        }

        public void CancelAllDownloads()
        {
            #if DEBUG
            if (MessageBox.Show("Cancel downloads?", "XIVLauncher", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            #endif

            foreach (var downloadService in DownloadServices)
            {
                try
                {
                    downloadService?.CancelAsync().GetAwaiter().GetResult();
                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not cancel download.");
                }
            }
        }

        private void RunDownloadQueue()
        {
            while (Downloads.Any(x => x.State == PatchState.Nothing))
            {
                Thread.Sleep(500);
                for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
                {
                    if (Slots[i] != SlotState.Done)
                        continue;

                    Slots[i] = SlotState.InProgress;

                    var toDl = Downloads.FirstOrDefault(x => x.State == PatchState.Nothing);

                    if (toDl == null)
                        return;

                    toDl.State = PatchState.IsDownloading;
                    var curIndex = i;
                    Task.Run(async () =>
                    {
                        try
                        {
                            await DownloadPatchAsync(toDl, curIndex);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Exception in DownloadPatchAsync");
                            throw;
                        }
                    });
                }
            }
        }

        private void CheckIsDone()
        {
            Log.Information("CheckIsDone!!");

            if (!Downloads.Any(x => x.State is PatchState.Nothing or PatchState.IsDownloading))
            {
                Log.Information("All patches downloaded.");

                DownloadsDone = true;

                for (var j = 0; j < Progresses.Length; j++)
                {
                    Progresses[j] = 0;
                }

                for (var j = 0; j < Speeds.Length; j++)
                {
                    Speeds[j] = 0;
                }

                return;
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

                Log.Information("Starting patch install for {0} at {1}({2})", toInstall.Patch.VersionId, toInstall.Patch.Url, CurrentInstallIndex);

                IsInstallerBusy = true;

                _installer.StartInstall(_gamePath, GetPatchFile(toInstall.Patch), toInstall.Patch, GetRepoForPatch(toInstall.Patch));

                while (_installer.State != PatchInstaller.InstallerState.Ready)
                {
                    Thread.Yield();
                }

                // TODO need to handle this better
                if (_installer.State == PatchInstaller.InstallerState.Failed)
                    return;

                Log.Information($"Patch at {CurrentInstallIndex} installed");

                IsInstallerBusy = false;

                toInstall.State = PatchState.Finished;
                CurrentInstallIndex++;
            }

            Log.Information("PATCHING finish");
            _installer.FinishInstall(_gamePath);

            IsSuccess = true;
            IsDone = true;
        }

        private enum HashCheckResult
        {
            Pass,
            BadHash,
            BadLength,
        }

        private static HashCheckResult CheckPatchValidity(PatchListEntry patchListEntry, FileInfo path)
        {
            if (patchListEntry.HashType != "sha1")
            {
                Log.Error("??? Unknown HashType: {0} for {1}", patchListEntry.HashType, patchListEntry.Url);
                return HashCheckResult.Pass;
            }

            var stream = path.OpenRead();

            if (stream.Length != patchListEntry.Length)
            {
                return HashCheckResult.BadLength;
            }

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
                return HashCheckResult.BadHash;
            }

            stream.Close();
            return HashCheckResult.Pass;
        }

        private FileInfo GetPatchFile(PatchListEntry patch)
        {
            var file = new FileInfo(Path.Combine(_patchStore.FullName, patch.GetFilePath()));
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

            if (patch.Url.Contains("ex4"))
                return Repository.Ex4;

            return Repository.Ffxiv;
        }

        private long GetDownloadLength() => GetDownloadLength(Downloads.Count);

        private long GetDownloadLength(int takeAmount) => Downloads.Take(takeAmount).Where(x => x.State == PatchState.Nothing || x.State == PatchState.IsDownloading).Sum(x => x.Patch.Length) - Progresses.Sum();    }
}