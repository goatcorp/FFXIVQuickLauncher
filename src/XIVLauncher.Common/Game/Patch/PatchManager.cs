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
using XIVLauncher.Common.Game.Patch.Acquisition.Aria;
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

        private readonly CancellationTokenSource _cancelTokenSource = new();

        private readonly AcquisitionMethod acquisitionMethod;
        private readonly long speedLimitBytes;
        private readonly Repository repo;
        private readonly DirectoryInfo gamePath;
        private readonly DirectoryInfo patchStore;
        private readonly PatchInstaller installer;
        private readonly Launcher launcher;
        private readonly string sid;

        private readonly Mutex downloadFinalizationLock = new Mutex();

        public readonly IReadOnlyList<PatchDownload> Downloads;

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

        private bool hasError = false;

        public event Action<PatchListEntry, string> OnFail;

        public enum FailReason
        {
            DownloadProblem,
            HashCheck,
        }

        public PatchManager(AcquisitionMethod acquisitionMethod, long speedLimitBytes, Repository repo, IEnumerable<PatchListEntry> patches, DirectoryInfo gamePath, DirectoryInfo patchStore, PatchInstaller installer, Launcher launcher, string sid)
        {
            Debug.Assert(patches != null, "patches != null ASSERTION FAILED");

            this.acquisitionMethod = acquisitionMethod;
            this.speedLimitBytes = speedLimitBytes;
            this.repo = repo;
            this.gamePath = gamePath;
            this.patchStore = patchStore;
            this.installer = installer;
            this.launcher = launcher;
            this.sid = sid;

            if (!this.patchStore.Exists)
                this.patchStore.Create();

            Downloads = patches.Select(patchListEntry => new PatchDownload {Patch = patchListEntry, State = PatchState.Nothing}).ToList().AsReadOnly();

            // All dl slots are available at the start
            for (var i = 0; i < MAX_DOWNLOADS_AT_ONCE; i++)
            {
                Slots[i] = SlotState.Done;
            }
        }

        public async Task PatchAsync(FileInfo aria2LogFile, bool external = true)
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

            await InitializeAcquisition(aria2LogFile).ConfigureAwait(false);

            try
            {
                await Task.WhenAll(new Task[] {
                    Task.Run(RunDownloadQueue, _cancelTokenSource.Token),
                    Task.Run(RunApplyQueue, _cancelTokenSource.Token),
                }).ConfigureAwait(false);
            }
            finally
            {
                // Only PatchManager uses Aria (or Torrent), so it's safe to shut it down here.
                await UnInitializeAcquisition().ConfigureAwait(false);
            }
        }

        public async Task InitializeAcquisition(FileInfo aria2LogFile)
        {
            // TODO: Come up with a better pattern for initialization. This sucks.
            switch (this.acquisitionMethod)
            {
                case AcquisitionMethod.NetDownloader:
                    // ignored
                    break;

                case AcquisitionMethod.Aria:
                    await AriaHttpPatchAcquisition.InitializeAsync(this.speedLimitBytes / MAX_DOWNLOADS_AT_ONCE, aria2LogFile);
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
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not uninitialize patch acquisition.");
            }
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

            if (outFile.Exists && CheckPatchValidity(download.Patch, outFile) == HashCheckResult.Pass)
            {
                download.State = PatchState.Downloaded;
                Slots[index] = SlotState.Done;
                Progresses[index] = download.Patch.Length;
                return;
            }

            PatchAcquisition acquisition;

            switch (this.acquisitionMethod)
            {
                case AcquisitionMethod.NetDownloader:
                    acquisition = new NetDownloaderPatchAcquisition(this.patchStore, this.speedLimitBytes / MAX_DOWNLOADS_AT_ONCE);
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
                void HandleError(string context)
                {
                    if (this.hasError)
                        return;

                    this.hasError = true;

                    CancelAllDownloads();
                    OnFail?.Invoke(download.Patch, context);

                    Task.Run(async () => await UnInitializeAcquisition(), new CancellationTokenSource(5000).Token).GetAwaiter().GetResult();

                    try
                    {
                        outFile.Delete();
                    }
                    catch (Exception ex)
                    {
                        // This is fine. We will catch it next try.
                        Log.Error(ex, "Could not delete patch file");
                    }

                    Environment.Exit(0);
                }

                if (args == AcquisitionResult.Error)
                {
                    Log.Error("Download failed for {VersionId}", download.Patch.VersionId);
                    HandleError("Download");
                    return;
                }

                if (args == AcquisitionResult.Cancelled)
                {
                    // Cancellation should not produce an error message, since it is always triggered by another error or the user.
                    Log.Error("Download cancelled for {0}", download.Patch.VersionId);

                    return;
                }

                // Indicate "Checking..."
                Slots[index] = SlotState.Checking;

                var checkResult = CheckPatchValidity(download.Patch, outFile);

                this.downloadFinalizationLock.WaitOne();

                // Let's just bail for now, need better handling of this later
                if (checkResult != HashCheckResult.Pass)
                {
                    Log.Error("CheckPatchValidity failed with {Result} for {VersionId} after DL", checkResult, download.Patch.VersionId);
                    HandleError($"ValidityCheck ({checkResult})");
                    return;
                }

                download.State = PatchState.Downloaded;
                Slots[index] = SlotState.Done;
                Progresses[index] = 0;
                Speeds[index] = 0;

                Log.Information("Patch at {0} downloaded completely", download.Patch.Url);

                this.CheckIsDone();
                this.downloadFinalizationLock.ReleaseMutex();
            };

            DownloadServices[index] = acquisition;

            await acquisition.StartDownloadAsync(realUrl, outFile);
        }

        public void CancelAllDownloads()
        {
            foreach (var downloadService in DownloadServices)
            {
                if (downloadService == null)
                    continue;

                try
                {
                    Task.Run(async () =>
                    {
                        await downloadService.CancelAsync();
                    }, new CancellationTokenSource(5000).Token).GetAwaiter().GetResult();
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

                Log.Information("Starting patch install for {0} at {1}({2})", toInstall.Patch.VersionId, toInstall.Patch.Url, CurrentInstallIndex);

                IsInstallerBusy = true;

                this.installer.StartInstall(this.gamePath, GetPatchFile(toInstall.Patch), toInstall.Patch);

                while (this.installer.State != PatchInstaller.InstallerState.Ready)
                {
                    Thread.Yield();
                }

                // TODO need to handle this better
                if (this.installer.State == PatchInstaller.InstallerState.Failed)
                    return;

                Log.Information($"Patch at {CurrentInstallIndex} installed");

                IsInstallerBusy = false;

                toInstall.State = PatchState.Finished;
                CurrentInstallIndex++;
            }

            Log.Information("PATCHING finish");
            this.installer.FinishInstall(this.gamePath);
        }

        private enum HashCheckResult
        {
            Pass,
            BadHash,
            BadLength,
            CannotParse,
            CrcMismatch,
            UnknownHashType,
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

            var parts = (int) Math.Ceiling((double) patchListEntry.Length / patchListEntry.HashBlockSize);
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

                return HashCheckResult.BadHash;
            }

            return HashCheckResult.Pass;
        }

        private FileInfo GetPatchFile(PatchListEntry patch)
        {
            var file = new FileInfo(Path.Combine(this.patchStore.FullName, patch.GetFilePath()));
            file.Directory.Create();

            return file;
        }

        private long GetDownloadLength() => GetDownloadLength(Downloads.Count);

        private long GetDownloadLength(int takeAmount) => Downloads.Take(takeAmount).Where(x => x.State == PatchState.Nothing || x.State == PatchState.IsDownloading).Sum(x => x.Patch.Length) - Progresses.Sum();    }
}
