using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Dalamud
{
    public class DalamudUpdater
    {
        private readonly DirectoryInfo addonDirectory;
        private readonly DirectoryInfo assetRootDirectory;
        private readonly IUniqueIdCache? cache;
        private readonly HttpClient client;

        private readonly ManualResetEventSlim updateFinished = new(false);

        private static readonly TimeSpan RetryBackoff = TimeSpan.FromSeconds(10);

        private readonly object runLock = new();
        private bool isRunning;

        private (string? BetaKind, string? BetaKey)? pendingRun;

        public event Action<DownloadState>? StateChanged;

        public event Action? PendingRunChanged;

        public string? PendingBetaKind
        {
            get
            {
                lock (this.runLock)
                {
                    return this.pendingRun?.BetaKind;
                }
            }
        }

        public DownloadState State
        {
            get;
            private set
            {
                if (field == value)
                    return;

                field = value;

                try
                {
                    StateChanged?.Invoke(field);
                }
                catch
                {
                    // ignored
                }
            }
        } = DownloadState.Unknown;

        public bool IsStaging { get; private set; } = false;

        public Exception? EnsurementException { get; private set; }

        private FileInfo? runnerInternal;

        public FileInfo Runner
        {
            get
            {
                if (RunnerOverride != null)
                    return RunnerOverride;

                return runnerInternal ?? throw new InvalidOperationException("Runner not prepared yet");
            }
            private set => runnerInternal = value;
        }

        public DirectoryInfo Runtime { get; }

        public FileInfo? RunnerOverride { get; set; }

        public DirectoryInfo? AssetDirectory { get; private set; }

        public IDalamudLoadingOverlay? Overlay { get; set; }

        public string? RolloutBucket { get; }

        public event Action<DalamudVersionInfo?>? ResolvedBranchChanged;

        public DalamudVersionInfo? ResolvedBranch
        {
            get;
            private set
            {
                if (field == value)
                    return;

                field = value;

                try
                {
                    ResolvedBranchChanged?.Invoke(field);
                }
                catch
                {
                    // ignored
                }
            }
        }

        public enum DownloadState
        {
            Unknown,
            Running,
            Done,
            NoIntegrity, // fail with error message
        }

        private enum RuntimeIntegrity
        {
            Valid,
            Invalid,
            Unknown, // hashes unavailable, can't tell
        }

        public DalamudUpdater(HttpClient client, DirectoryInfo addonDirectory, DirectoryInfo runtimeDirectory, DirectoryInfo assetRootDirectory, IUniqueIdCache? cache, string? dalamudRolloutBucket)
        {
            this.addonDirectory = addonDirectory;
            this.assetRootDirectory = assetRootDirectory;
            this.client = client;

            this.Runtime = runtimeDirectory;
            this.AssetDirectory = null;
            this.cache = cache;

            this.RolloutBucket = dalamudRolloutBucket;

            if (this.RolloutBucket == null)
            {
                var rng = new Random();
                this.RolloutBucket = rng.Next(0, 9) >= 7 ? "Canary" : "Control";
            }
        }

        public void SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep progress)
        {
            Overlay!.SetStep(progress);
        }

        public void ShowOverlay()
        {
            Overlay!.SetVisible();
        }

        public void CloseOverlay()
        {
            Overlay!.SetInvisible();
        }

        private void ReportOverlayProgress(long? size, long downloaded, double? progress)
        {
            Overlay!.ReportProgress(size, downloaded, progress);
        }

        /// <summary>
        /// Start updating to the given branch. While an update runs, the request is buffered and started once that update ends.
        /// </summary>
        public void Run(string? betaKind, string? betaKey)
        {
            var isQueued = false;

            lock (this.runLock)
            {
                if (this.isRunning)
                {
                    Log.Information("[DUPDATE] Update running, queueing {Kind}", betaKind);
                    this.pendingRun = (betaKind, betaKey);
                    isQueued = true;
                }
                else
                {
                    Log.Information("[DUPDATE] Starting...");
                    this.isRunning = true;
                    this.State = DownloadState.Running;
                    this.updateFinished.Reset();
                }
            }

            if (isQueued)
            {
                this.RaisePendingRunChanged();
                return;
            }

            Task.Run(() => this.RunQueue(betaKind, betaKey));
        }

        private void RaisePendingRunChanged()
        {
            try
            {
                PendingRunChanged?.Invoke();
            }
            catch
            {
                // ignored
            }
        }

        private async Task RunQueue(string? betaKind, string? betaKey)
        {
            while (true)
            {
                this.ResolvedBranch = null;
                this.IsStaging = false;
                this.EnsurementException = null;

                var isUpdated = await this.TryUpdate(betaKind, betaKey).ConfigureAwait(false);

                lock (this.runLock)
                {
                    if (this.pendingRun is not { } next)
                    {
                        this.isRunning = false;
                        this.State = isUpdated ? DownloadState.Done : DownloadState.NoIntegrity;
                        this.updateFinished.Set();
                        return;
                    }

                    this.pendingRun = null;
                    (betaKind, betaKey) = next;
                }

                Log.Information("[DUPDATE] Starting queued update for {Kind}", betaKind);
                this.RaisePendingRunChanged();
            }
        }

        private async Task<bool> TryUpdate(string? betaKind, string? betaKey)
        {
            const int MAX_TRIES = 10;

            for (var tries = 0; tries < MAX_TRIES; tries++)
            {
                try
                {
                    await UpdateDalamud(betaKind, betaKey).ConfigureAwait(true);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DUPDATE] Update failed, try {TryCnt}/{MaxTries}...", tries + 1, MAX_TRIES);
                    this.EnsurementException = ex;
                }

                if (this.HasPendingRun())
                {
                    return false;
                }

                // No wait after the last try
                if (tries < MAX_TRIES - 1)
                {
                    await Task.Delay(RetryBackoff).ConfigureAwait(false);
                }
            }

            return false;
        }

        private bool HasPendingRun()
        {
            lock (this.runLock)
            {
                return this.pendingRun != null;
            }
        }

        public void WaitForUpdate()
        {
            this.updateFinished.Wait();
        }

        public bool? ReCheckVersion(DirectoryInfo gamePath)
        {
            if (this.State != DownloadState.Done)
                return null;

            if (this.RunnerOverride != null)
                return true;

            var info = DalamudVersionInfo.Load(new FileInfo(Path.Combine(this.Runner.DirectoryName!,
                "version.json")));

            return Repository.Ffxiv.GetVer(gamePath) == info.SupportedGameVer;
        }

        private static string GetBetaTrackName(string betaKind) =>
            string.IsNullOrEmpty(betaKind) ? "staging" : betaKind;

        private async Task<(DalamudVersionInfo? release, DalamudVersionInfo? staging)> GetVersionInfo(string? betaKind, string? betaKey)
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, DalamudLauncher.REMOTE_BASE + $"release&bucket={this.RolloutBucket}");
            message.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true
            };

            using var versionInfoResponse = await this.client.SendAsync(message).ConfigureAwait(false);
            versionInfoResponse.EnsureSuccessStatusCode();

            var versionInfoRelease = JsonConvert.DeserializeObject<DalamudVersionInfo>(await versionInfoResponse.Content.ReadAsStringAsync());

            DalamudVersionInfo? versionInfoStaging = null;

            if (!string.IsNullOrEmpty(betaKey) && !string.IsNullOrEmpty(betaKind))
            {
                using var versionInfoJsonStaging = await this.client.GetAsync(DalamudLauncher.REMOTE_BASE + GetBetaTrackName(betaKind)).ConfigureAwait(false);

                if (versionInfoJsonStaging.IsSuccessStatusCode)
                {
                    versionInfoStaging = JsonConvert.DeserializeObject<DalamudVersionInfo>(await versionInfoJsonStaging.Content.ReadAsStringAsync().ConfigureAwait(false));
                }
                else if (versionInfoJsonStaging.StatusCode != HttpStatusCode.BadRequest)
                {
                    Log.Warning("[DUPDATE] Could not get version info for track {Track}: {StatusCode}", GetBetaTrackName(betaKind), versionInfoJsonStaging.StatusCode);
                }
            }

            return (versionInfoRelease, versionInfoStaging);
        }

        private async Task UpdateDalamud(string? betaKind, string? betaKey)
        {
            var (versionInfoRelease, versionInfoStaging) = await GetVersionInfo(betaKind, betaKey).ConfigureAwait(false);

            if (versionInfoRelease == null)
                throw new Exception("Release Dalamud version info deserialized to null");

            var remoteVersionInfo = versionInfoRelease;

            if (versionInfoStaging?.Key != null && versionInfoStaging.Key == betaKey)
            {
                remoteVersionInfo = versionInfoStaging;
                IsStaging = true;
                Log.Information("[DUPDATE] Using staging version {Kind} with key {Key} ({Hash})", betaKind, betaKey, remoteVersionInfo.AssemblyVersion);
            }
            else
            {
                Log.Information("[DUPDATE] Using release version ({Hash})", remoteVersionInfo.AssemblyVersion);
            }

            // Update resolved branch to reflect what the server actually selected
            this.ResolvedBranch = remoteVersionInfo;

            var versionInfoJson = JsonConvert.SerializeObject(remoteVersionInfo);

            var addonPath = new DirectoryInfo(Path.Combine(this.addonDirectory.FullName, "Hooks"));
            var currentVersionPath = new DirectoryInfo(Path.Combine(addonPath.FullName, remoteVersionInfo.AssemblyVersion));
            var runtimePaths = new DirectoryInfo[]
            {
                new(Path.Combine(this.Runtime.FullName, "host", "fxr", remoteVersionInfo.RuntimeVersion)),
                new(Path.Combine(this.Runtime.FullName, "shared", "Microsoft.NETCore.App", remoteVersionInfo.RuntimeVersion)),
                new(Path.Combine(this.Runtime.FullName, "shared", "Microsoft.WindowsDesktop.App", remoteVersionInfo.RuntimeVersion)),
            };

            if (!currentVersionPath.Exists || !IsIntegrity(currentVersionPath))
            {
                Log.Information("[DUPDATE] Not found, redownloading");

                SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Dalamud);

                try
                {
                    await DownloadDalamud(currentVersionPath, remoteVersionInfo).ConfigureAwait(true);
                    CleanUpOld(addonPath, remoteVersionInfo.AssemblyVersion);

                    // This is a good indicator that we should clear the UID cache
                    cache?.Reset();
                }
                catch (Exception ex)
                {
                    throw new DalamudIntegrityException("Could not download Dalamud", ex);
                }
            }

            if (remoteVersionInfo.RuntimeRequired)
            {
                Log.Information("[DUPDATE] Now starting for .NET Runtime {0}", remoteVersionInfo.RuntimeVersion);

                var versionFile = new FileInfo(Path.Combine(this.Runtime.FullName, "version"));
                var localVersion = GetLocalRuntimeVersion(versionFile);

                var runtimeNeedsUpdate = localVersion != remoteVersionInfo.RuntimeVersion;

                if (!this.Runtime.Exists)
                    Directory.CreateDirectory(this.Runtime.FullName);

                var runtimeIntegrity = RuntimeIntegrity.Unknown;

                // Only check runtime hashes if we don't need to update it
                if (!runtimeNeedsUpdate)
                {
                    try
                    {
                        runtimeIntegrity = await CheckRuntimeHashes(Runtime, localVersion).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DUPDATE] Could not check runtime integrity.");
                    }
                }

                if (runtimePaths.Any(p => !p.Exists) || runtimeNeedsUpdate || runtimeIntegrity == RuntimeIntegrity.Invalid)
                {
                    Log.Information("[DUPDATE] Not found, outdated or no integrity: {LocalVer} - {RemoteVer}", localVersion, remoteVersionInfo.RuntimeVersion);

                    SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Runtime);

                    try
                    {
                        Log.Verbose("[DUPDATE] Now download runtime...");
                        await DownloadRuntime(this.Runtime, remoteVersionInfo.RuntimeVersion).ConfigureAwait(false);

                        File.WriteAllText(versionFile.FullName, remoteVersionInfo.RuntimeVersion);

                        var downloadIntegrity = await CheckRuntimeHashes(this.Runtime, remoteVersionInfo.RuntimeVersion).ConfigureAwait(false);

                        if (downloadIntegrity == RuntimeIntegrity.Invalid)
                        {
                            throw new DalamudIntegrityException("No runtime integrity after download");
                        }

                        if (downloadIntegrity == RuntimeIntegrity.Unknown)
                        {
                            Log.Warning("[DUPDATE] Could not verify runtime {Version}, rechecking on next launch", remoteVersionInfo.RuntimeVersion);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new DalamudIntegrityException("Could not ensure runtime", ex);
                    }
                }

                CleanUpOldRuntimes(runtimePaths);
            }

            Log.Verbose("[DUPDATE] Now ensure assets...");

            int assetVer;

            try
            {
                this.SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Assets);
                this.ReportOverlayProgress(null, 0, null);
                var assetResult = await AssetManager.EnsureAssets(this.client, this, this.assetRootDirectory).ConfigureAwait(true);
                AssetDirectory = assetResult.AssetDir;
                assetVer = assetResult.Version;
            }
            catch (Exception ex)
            {
                throw new DalamudIntegrityException("Could not ensure assets", ex);
            }

            if (!IsIntegrity(currentVersionPath))
            {
                throw new DalamudIntegrityException("No integrity after ensurement");
            }

            WriteVersionJson(currentVersionPath, versionInfoJson);

            Log.Information("[DUPDATE] All set for {GameVersion} with {DalamudVersion}({RuntimeVersion}, {AssetVersion})", remoteVersionInfo.SupportedGameVer, remoteVersionInfo.AssemblyVersion, remoteVersionInfo.RuntimeVersion, assetVer);

            Runner = new FileInfo(Path.Combine(currentVersionPath.FullName, "Dalamud.Injector.exe"));
            SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Starting);
            ReportOverlayProgress(null, 0, null);
        }

        private static bool CanRead(FileInfo info)
        {
            try
            {
                using var stream = info.OpenRead();
                stream.ReadByte();
            }
            catch
            {
                return false;
            }

            return true;
        }

        private static bool IsIntegrity(DirectoryInfo addonPath)
        {
            var files = addonPath.GetFiles();

            try
            {
                if (!CanRead(files.First(x => x.Name == "Dalamud.Injector.exe"))
                    || !CanRead(files.First(x => x.Name == "Dalamud.dll"))
                    || !CanRead(files.First(x => x.Name == "ImGuiScene.dll")))
                {
                    Log.Error("[DUPDATE] Can't open files for read");
                    return false;
                }

                var hashesPath = Path.Combine(addonPath.FullName, "hashes.json");

                if (!File.Exists(hashesPath))
                {
                    Log.Error("[DUPDATE] No hashes.json");
                    return false;
                }

                return CheckIntegrity(addonPath, File.ReadAllText(hashesPath));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] No dalamud integrity");
                return false;
            }
        }

        private static bool CheckIntegrity(DirectoryInfo directory, string hashesJson)
        {
            try
            {
                Log.Verbose("[DUPDATE] Checking integrity of {Directory}", directory.FullName);

                var hashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(hashesJson);

                if (hashes == null)
                {
                    throw new Exception("Hashes deserialized to null");
                }

                foreach (var hash in hashes)
                {
                    var file = Path.Combine(directory.FullName, hash.Key.Replace("\\", "/"));
                    using var fileStream = File.OpenRead(file);
                    using var md5 = MD5.Create();

                    var hashed = BitConverter.ToString(md5.ComputeHash(fileStream)).ToUpperInvariant().Replace("-", string.Empty);

                    if (hashed != hash.Value)
                    {
                        Log.Error("[DUPDATE] Integrity check failed for {0} ({1} - {2})", file, hash.Value, hashed);
                        return false;
                    }

                    Log.Verbose("[DUPDATE] Integrity check OK for {0} ({1})", file, hashed);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] Integrity check failed");
                return false;
            }

            return true;
        }

        private static void CleanUpOld(DirectoryInfo addonPath, string currentVer)
        {
            if (GameHelpers.CheckIsGameOpen())
                return;

            if (!addonPath.Exists)
                return;

            foreach (var directory in addonPath.GetDirectories())
            {
                if (directory.Name == "dev" || directory.Name == currentVer) continue;

                try
                {
                    PlatformHelpers.ForceDeleteDirectory(directory);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static void CleanUpOldRuntimes(DirectoryInfo[] currentRuntimePaths)
        {
            if (GameHelpers.CheckIsGameOpen())
            {
                return;
            }

            foreach (var current in currentRuntimePaths)
            {
                if (current.Parent is not { Exists: true } versionsDir)
                {
                    continue;
                }

                foreach (var directory in versionsDir.GetDirectories())
                {
                    if (directory.Name == current.Name)
                    {
                        continue;
                    }

                    try
                    {
                        PlatformHelpers.ForceDeleteDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[DUPDATE] Could not clean up old runtime {Path}", directory.FullName);
                    }
                }
            }
        }

        private static void WriteVersionJson(DirectoryInfo addonPath, string info)
        {
            File.WriteAllText(Path.Combine(addonPath.FullName, "version.json"), info);
        }

        private async Task DownloadDalamud(DirectoryInfo addonPath, DalamudVersionInfo version)
        {
            // Ensure directory exists
            if (addonPath.Exists)
            {
                if (GameHelpers.CheckIsGameOpen())
                {
                    throw new DalamudIntegrityException("Cannot replace Dalamud while the game is running");
                }

                PlatformHelpers.ForceDeleteDirectory(addonPath);
            }

            addonPath.Create();

            var downloadPath = PlatformHelpers.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            try
            {
                await this.DownloadFile(version.DownloadUrl, downloadPath).ConfigureAwait(false);
                ZipFile.ExtractToDirectory(downloadPath, addonPath.FullName);
            }
            finally
            {
                PlatformHelpers.DeleteTempFile(downloadPath);
            }

            try
            {
                var devPath = new DirectoryInfo(Path.Combine(addonPath.FullName, "..", "dev"));

                PlatformHelpers.DeleteAndRecreateDirectory(devPath);
                PlatformHelpers.CopyFilesRecursively(addonPath, devPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] Could not copy to dev folder.");
            }
        }

        private string GetLocalRuntimeVersion(FileInfo versionFile)
        {
            // This is the version we first shipped. We didn't write out a version file, so we can't check it.
            var localVersion = "5.0.6";

            try
            {
                if (versionFile.Exists)
                    localVersion = File.ReadAllText(versionFile.FullName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] Could not read local runtime version.");
            }

            return localVersion;
        }

        private async Task<RuntimeIntegrity> CheckRuntimeHashes(DirectoryInfo runtimePath, string version)
        {
            var hashesFile = new FileInfo(Path.Combine(runtimePath.FullName, $"hashes-{version}.json"));
            string? runtimeHashes;

            if (!hashesFile.Exists)
            {
                Log.Verbose("[DUPDATE] Hashes file does not exist, redownloading...");

                try
                {
                    runtimeHashes = await this.client.GetStringAsync($"https://kamori.goats.dev/Dalamud/Release/Runtime/Hashes/{version}").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DUPDATE] Could not download hashes for runtime v{Version}", version);
                    return RuntimeIntegrity.Unknown;
                }

                if (!IsValidHashes(runtimeHashes))
                {
                    Log.Error("[DUPDATE] Invalid hashes for runtime v{Version}", version);
                    return RuntimeIntegrity.Unknown;
                }

                File.WriteAllText(hashesFile.FullName, runtimeHashes);
            }
            else
            {
                runtimeHashes = File.ReadAllText(hashesFile.FullName);
            }

            return CheckIntegrity(runtimePath, runtimeHashes) ? RuntimeIntegrity.Valid : RuntimeIntegrity.Invalid;
        }

        private static bool IsValidHashes(string hashesJson)
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(hashesJson) is { Count: > 0 };
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private async Task DownloadRuntime(DirectoryInfo runtimePath, string version)
        {
            // Ensure directory exists
            if (!runtimePath.Exists)
            {
                runtimePath.Create();
            }
            else if (GameHelpers.CheckIsGameOpen())
            {
                Log.Information("[DUPDATE] Installing runtime {Version} side by side", version);
            }
            else
            {
                PlatformHelpers.ForceDeleteDirectory(runtimePath);
                runtimePath.Create();
            }

            // Wait for it to be gone, thanks Windows
            await Task.Delay(1000).ConfigureAwait(false);

            var dotnetUrl = $"https://kamori.goats.dev/Dalamud/Release/Runtime/DotNet/{version}";
            var desktopUrl = $"https://kamori.goats.dev/Dalamud/Release/Runtime/WindowsDesktop/{version}";

            var downloadPath = PlatformHelpers.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            try
            {
                await this.DownloadFile(dotnetUrl, downloadPath).ConfigureAwait(false);
                ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName, overwriteFiles: true);

                await this.DownloadFile(desktopUrl, downloadPath).ConfigureAwait(false);
                ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName, overwriteFiles: true);
            }
            finally
            {
                PlatformHelpers.DeleteTempFile(downloadPath);
            }
        }

        public async Task DownloadFile(string url, string path)
        {
            var downloader = new HttpClientDownloadWithProgress(this.client, url, path);
            downloader.ProgressChanged += this.ReportOverlayProgress;

            await downloader.Download().ConfigureAwait(false);
        }
    }

    public class DalamudIntegrityException(string msg, Exception? inner = null) : Exception(msg, inner);
}
