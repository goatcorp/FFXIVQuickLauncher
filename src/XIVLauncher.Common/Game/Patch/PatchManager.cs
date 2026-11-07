using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game.Patch
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

        private static readonly TimeSpan CancelDownloadsTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan CancelInstallTimeout = TimeSpan.FromMinutes(15);

        private readonly CancellationTokenSource _cancelTokenSource = new();

        private readonly IPatchAcquisition acquisition;
        private readonly Repository repo;
        private readonly DirectoryInfo gamePath;
        private readonly DirectoryInfo patchStore;
        private readonly PatchInstaller installer;
        private readonly Launcher launcher;
        private readonly string sid;

        private readonly object downloadFinalizationLock = new();

        public readonly IReadOnlyList<PatchDownload> Downloads;

        private long speedLimitBps;

        public int CurrentInstallIndex { get; private set; }

        public enum SlotState
        {
            InProgress,
            Checking,
            Done,
        }

        public readonly long[] Progresses = new long[MAX_DOWNLOADS_AT_ONCE];
        public readonly double[] Speeds = new double[MAX_DOWNLOADS_AT_ONCE];
        public readonly PatchDownload?[] Actives = new PatchDownload[MAX_DOWNLOADS_AT_ONCE];
        public readonly SlotState[] Slots = new SlotState[MAX_DOWNLOADS_AT_ONCE];
        public readonly PatchAcquisitionTask?[] AcquisitionTasks = new PatchAcquisitionTask[MAX_DOWNLOADS_AT_ONCE];

        public bool IsInstallerBusy { get; private set; }

        public bool DownloadsDone { get; private set; }

        public bool IsCancelling { get; private set; }

        public long AllDownloadsLength => GetDownloadLength();

        private int hasError;

        public event Action<PatchListEntry, string>? OnFail;

        public PatchManager(IPatchAcquisition acquisition, long speedLimitBps, Repository repo, IEnumerable<PatchListEntry> patches, DirectoryInfo gamePath, DirectoryInfo patchStore, PatchInstaller installer, Launcher launcher, string sid)
        {
            Debug.Assert(patches != null);

            this.acquisition = acquisition;
            this.speedLimitBps = speedLimitBps;
            this.repo = repo;
            this.gamePath = gamePath;
            this.patchStore = patchStore;
            this.installer = installer;
            this.launcher = launcher;
            this.sid = sid;

            if (!this.patchStore.Exists)
                this.patchStore.Create();

            Downloads = patches.Select(patchListEntry => new PatchDownload { Patch = patchListEntry, State = PatchState.Nothing }).ToList().AsReadOnly();

            // All dl slots are available at the start
            for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
            {
                Slots[i] = SlotState.Done;
            }
        }

        public async Task<bool> PatchAsync(bool external = true)
        {
            if (!EnvironmentSettings.IsIgnoreSpaceRequirements)
            {
                var freeSpaceDownload = PlatformHelpers.GetDiskFreeSpace(this.patchStore);

                if (Downloads.Any(x => x.Patch.Length > freeSpaceDownload))
                {
                    throw new NotEnoughSpaceException(NotEnoughSpaceException.SpaceKind.Patches,
                        Downloads.OrderByDescending(x => x.Patch.Length).First().Patch.Length, freeSpaceDownload);
                }

                // If the first 6 patches altogether are bigger than the patch drive, we might run out of space
                if (freeSpaceDownload < GetDownloadLength(6))
                {
                    throw new NotEnoughSpaceException(NotEnoughSpaceException.SpaceKind.AllPatches, AllDownloadsLength,
                        freeSpaceDownload);
                }

                var freeSpaceGame = PlatformHelpers.GetDiskFreeSpace(this.gamePath);

                if (freeSpaceGame < AllDownloadsLength)
                {
                    throw new NotEnoughSpaceException(NotEnoughSpaceException.SpaceKind.Game, AllDownloadsLength,
                        freeSpaceGame);
                }
            }

            this.installer.StartIfNeeded(external);
            this.installer.WaitOnHello();

            await this.acquisition.StartIfNeededAsync(this.speedLimitBps);

            await Task.WhenAll(new[]
            {
                Task.Run(() => RunQueue(RunDownloadQueue), _cancelTokenSource.Token),
                Task.Run(() => RunQueue(RunApplyQueue), _cancelTokenSource.Token),
            }).ConfigureAwait(false);

            return !this.IsCancelling && this.installer.State != PatchInstaller.InstallerState.Failed;
        }

        private void RunQueue(Action queue)
        {
            try
            {
                queue();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Patch queue failed");
                StartCancellation();
                throw;
            }
        }

        public void StartCancellation()
        {
            this.IsCancelling = true;
            this.CancelAllDownloads();
        }

        public async Task SetSpeedLimitAsync(long bytesPerSecond)
        {
            this.speedLimitBps = bytesPerSecond;
            await this.acquisition.SetGlobalSpeedLimitAsync(bytesPerSecond);
        }

        private async Task DownloadPatchAsync(PatchDownload download, int index)
        {
            var outFile = GetPatchFile(download.Patch);

            var realUrl = download.Patch.Url;

            if (this.repo != Repository.Boot && false) // Disabled for now, waiting on SE to patch this
            {
                realUrl = await this.launcher.GenPatchToken(download.Patch.Url, this.sid);
            }

            Log.Information("Downloading patch {0} at {1} to {2}", download.Patch.VersionId, realUrl, outFile.FullName);

            Actives[index] = download;

            // Skip this patch immediately if it's already downloaded and checks out
            if (outFile.Exists && CheckPatchValidity(download.Patch, outFile) == HashCheckResult.Pass)
            {
                lock (this.downloadFinalizationLock)
                {
                    download.State = PatchState.Downloaded;
                    Slots[index] = SlotState.Done;
                    Progresses[index] = 0;
                    this.CheckIsDone();
                }

                return;
            }

            var acquisitionTask = this.acquisition.MakeTask(realUrl, outFile);
            acquisitionTask.ProgressChanged += (_, args) =>
            {
                Progresses[index] = args.Progress;
                Speeds[index] = args.BytesPerSecondSpeed;
            };

            acquisitionTask.Complete += (_, args) =>
            {
                if (args == AcquisitionResult.Error)
                {
                    Log.Error("Download failed for {VersionId}", download.Patch.VersionId);
                    HandleError(download.Patch, "Download");
                    return;
                }

                if (args == AcquisitionResult.Cancelled)
                {
                    // Cancellation should not produce an error message, since it is always triggered by another error or the user.
                    Log.Error("Download cancelled for {0}", download.Patch.VersionId);

                    return;
                }

                // Indicate "Checking..."
                lock (this.downloadFinalizationLock)
                {
                    Slots[index] = SlotState.Checking;
                }

                var checkResult = CheckPatchValidity(download.Patch, outFile);

                // Let's just bail for now, need better handling of this later
                if (checkResult != HashCheckResult.Pass)
                {
                    Log.Error("CheckPatchValidity failed with {Result} for {VersionId} after DL", checkResult, download.Patch.VersionId);
                    HandleError(download.Patch, $"ValidityCheck ({checkResult})");
                    return;
                }

                lock (this.downloadFinalizationLock)
                {
                    download.State = PatchState.Downloaded;
                    Slots[index] = SlotState.Done;
                    Progresses[index] = 0;
                    Speeds[index] = 0;

                    Log.Information("Patch at {0} downloaded completely", download.Patch.Url);

                    this.CheckIsDone();
                }
            };

            this.AcquisitionTasks[index] = acquisitionTask;

            if (this.IsCancelling)
            {
                Log.Information("Patching cancelled, not starting download of {VersionId}", download.Patch.VersionId);
                return;
            }

            await acquisitionTask.StartAsync();
        }

        private void HandleError(PatchListEntry patch, string context)
        {
            if (Interlocked.Exchange(ref this.hasError, 1) == 1)
            {
                return;
            }

            this.IsCancelling = true;
            CancelAllDownloads();
            OnFail?.Invoke(patch, context);
        }

        private void CancelAllDownloads()
        {
            var cancels = this.AcquisitionTasks
                .Where(task => task != null)
                .Select(task => Task.Run(task!.CancelAsync))
                .ToArray();

            try
            {
                if (!Task.WaitAll(cancels, CancelDownloadsTimeout))
                {
                    Log.Error("Timed out cancelling downloads");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not cancel download.");
            }
        }

        private void RunDownloadQueue()
        {
            while (Downloads.Any(x => x.State == PatchState.Nothing))
            {
                Thread.Sleep(200);

                if (this.IsCancelling)
                {
                    Log.Information("Patching cancelled, exiting download loop.");
                    return;
                }

                for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
                {
                    PatchDownload? toDl;

                    lock (this.downloadFinalizationLock)
                    {
                        if (Slots[i] != SlotState.Done)
                        {
                            continue;
                        }

                        toDl = Downloads.FirstOrDefault(x => x.State == PatchState.Nothing);

                        if (toDl == null)
                        {
                            return;
                        }

                        Slots[i] = SlotState.InProgress;
                        toDl.State = PatchState.IsDownloading;
                    }

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
                            HandleError(toDl.Patch, $"Exception ({ex.GetType().Name})");
                        }
                    });
                }
            }
        }

        private void CheckIsDone()
        {
            if (!Downloads.Any(x => x.State is PatchState.Nothing or PatchState.IsDownloading))
            {
                Log.Information("All patches downloaded.");

                DownloadsDone = true;

                for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
                {
                    Slots[i] = SlotState.Done;
                    Progresses[i] = 0;
                    Speeds[i] = 0;
                }
            }
        }

        private void RunApplyQueue()
        {
            while (CurrentInstallIndex < Downloads.Count)
            {
                Thread.Sleep(200);

                if (this.IsCancelling)
                {
                    if (this.installer.State == PatchInstaller.InstallerState.Busy)
                    {
                        // Wait for the installer to finish what it's doing
                        continue;
                    }

                    Log.Information("Patching cancelled, exiting apply loop.");
                    return;
                }

                var toInstall = Downloads[CurrentInstallIndex];

                if (toInstall.State != PatchState.Downloaded)
                    continue;

                toInstall.State = PatchState.IsInstalling;

                Log.Information("Starting patch install for {0} at {1}({2})", toInstall.Patch.VersionId, toInstall.Patch.Url, CurrentInstallIndex);

                IsInstallerBusy = true;

                this.installer.StartInstall(this.gamePath, GetPatchFile(toInstall.Patch), toInstall.Patch);

                var cancelWait = new Stopwatch();

                while (this.installer.State == PatchInstaller.InstallerState.Busy)
                {
                    Thread.Sleep(100);

                    if (!this.IsCancelling)
                    {
                        continue;
                    }

                    if (!cancelWait.IsRunning)
                    {
                        cancelWait.Start();
                    }

                    if (cancelWait.Elapsed > CancelInstallTimeout)
                    {
                        Log.Error("Install of {VersionId} did not finish after cancelling", toInstall.Patch.VersionId);
                        this.installer.Abort();
                    }
                }

                // Stop the download queue to make sure that PatchAsync completes
                if (this.installer.State == PatchInstaller.InstallerState.Failed)
                {
                    this.IsCancelling = true;
                    return;
                }

                Log.Information($"Patch at {CurrentInstallIndex} installed");

                IsInstallerBusy = false;

                toInstall.State = PatchState.Finished;
                CurrentInstallIndex++;
            }

            Log.Information("Now finalize install");
            this.installer.FinalizeAndWait(this.gamePath);
            Log.Information("RunApplyQueue done");
        }

        private enum HashCheckResult
        {
            Pass,
            BadHash,
            BadLength,
            CannotParse,
            CrcMismatch,
            UnknownHashType,
            BadHashList,
        }

        private static HashCheckResult CheckPatchValidity(PatchListEntry patchListEntry, FileInfo path)
        {
            if (patchListEntry.HashType != "sha1")
            {
                // Boot patches do not have a hash. We can parse them here to see if they are valid.
                if (patchListEntry.GetRepo() == Repository.Boot)
                {
                    try
                    {
                        using var fileStream = path.OpenRead();
                        using var patch = new ZiPatchFile(fileStream, true);

                        foreach (var chunk in patch.GetChunks())
                        {
                            if (!chunk.IsChecksumValid)
                            {
                                Log.Error("Boot patch {Patch} has invalid checksum in {ChunkType} chunk", patchListEntry, chunk.ChunkType);
                                return HashCheckResult.CrcMismatch;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Could not parse boot patch {Patch}", patchListEntry);
                        return HashCheckResult.CannotParse;
                    }

                    return HashCheckResult.Pass;
                }

                Log.Error("??? Unknown HashType: {0} for {1}", patchListEntry.HashType, patchListEntry.Url);
                return HashCheckResult.UnknownHashType;
            }

            using var stream = path.OpenRead();

            if (stream.Length != patchListEntry.Length)
            {
                return HashCheckResult.BadLength;
            }

            // Boot patches don't have hashes but they never get here because they don't set a hash type
            if (patchListEntry.HashBlockSize <= 0 || patchListEntry.Hashes == null)
            {
                Log.Error("Patch list has no hashes for {VersionId}", patchListEntry.VersionId);
                return HashCheckResult.BadHashList;
            }

            var blockSize = (int)patchListEntry.HashBlockSize;
            var parts = (int)Math.Ceiling((double)patchListEntry.Length / blockSize);

            if (patchListEntry.Hashes.Length != parts)
            {
                Log.Error("Patch list has {Count} hashes for {Parts} blocks of {VersionId}", patchListEntry.Hashes.Length, parts, patchListEntry.VersionId);
                return HashCheckResult.BadHashList;
            }

            var block = new byte[blockSize];

            for (var i = 0; i < parts; i++)
            {
                var read = stream.ReadAtLeast(block, blockSize, throwOnEndOfStream: false);

                using var sha1 = SHA1.Create();

                var hash = sha1.ComputeHash(block, 0, read);
                var sb = new StringBuilder(hash.Length * 2);

                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }

                if (sb.ToString() == patchListEntry.Hashes[i])
                    continue;

                return HashCheckResult.BadHash;
            }

            return HashCheckResult.Pass;
        }

        private FileInfo GetPatchFile(PatchListEntry patch)
        {
            var file = new FileInfo(Path.Combine(this.patchStore.FullName, patch.GetFilePath()));
            file.Directory!.Create();

            return file;
        }

        private long GetDownloadLength() => GetDownloadLength(Downloads.Count);

        private long GetDownloadLength(int takeAmount) => Downloads.Take(takeAmount).Where(x => x.State == PatchState.Nothing || x.State == PatchState.IsDownloading).Sum(x => x.Patch.Length) - Progresses.Sum();
    }
}
