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

#nullable enable

namespace XIVLauncher.Common.Dalamud
{
    public class DalamudUpdater
    {
        private readonly DirectoryInfo addonDirectory;
        private readonly DirectoryInfo assetDirectory;
        private readonly DirectoryInfo configDirectory;
        private readonly IUniqueIdCache? cache;

        private readonly TimeSpan defaultTimeout = TimeSpan.FromMinutes(15);

        private bool forceProxy = false;

        public DownloadState State { get; private set; } = DownloadState.Unknown;
        public bool IsStaging { get; private set; } = false;

        public Exception? EnsurementException { get; private set; }

        private FileInfo runnerInternal;

        public FileInfo Runner
        {
            get
            {
                if (RunnerOverride != null)
                    return RunnerOverride;

                return runnerInternal;
            }
            private set => runnerInternal = value;
        }

        public DirectoryInfo Runtime { get; }

        public FileInfo? RunnerOverride { get; set; }

        public DirectoryInfo AssetDirectory { get; private set; }

        public IDalamudLoadingOverlay? Overlay { get; set; }

        public string? RolloutBucket { get; }

        public enum DownloadState
        {
            Unknown,
            Done,
            NoIntegrity, // fail with error message
        }

        public DalamudUpdater(DirectoryInfo addonDirectory, DirectoryInfo runtimeDirectory, DirectoryInfo assetDirectory, DirectoryInfo configDirectory, IUniqueIdCache? cache, string? dalamudRolloutBucket)
        {
            this.addonDirectory = addonDirectory;
            this.Runtime = runtimeDirectory;
            this.assetDirectory = assetDirectory;
            this.configDirectory = configDirectory;
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

        public void Run(bool overrideForceProxy = false)
        {
            Log.Information("[DUPDATE] Starting... (forceProxy: {ForceProxy})", overrideForceProxy);
            this.State = DownloadState.Unknown;

            this.forceProxy = overrideForceProxy;

            Task.Run(async () =>
            {
                const int MAX_TRIES = 10;

                var isUpdated = false;

                for (var tries = 0; tries < MAX_TRIES; tries++)
                {
                    try
                    {
                        await UpdateDalamud().ConfigureAwait(true);
                        isUpdated = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DUPDATE] Update failed, try {TryCnt}/{MaxTries}...", tries, MAX_TRIES);
                        this.EnsurementException = ex;
                        this.forceProxy = true;
                    }
                }

                this.State = isUpdated ? DownloadState.Done : DownloadState.NoIntegrity;
            });
        }

        private static string GetBetaTrackName(DalamudSettings settings) =>
            string.IsNullOrEmpty(settings.DalamudBetaKind) ? "staging" : settings.DalamudBetaKind;

        private async Task<(DalamudVersionInfo release, DalamudVersionInfo? staging)> GetVersionInfo(DalamudSettings settings)
        {
            using var client = new HttpClient
            {
                Timeout = this.defaultTimeout,
            };

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
            };

            var versionInfoJsonRelease = await client.GetStringAsync(DalamudLauncher.REMOTE_BASE + $"release&bucket={this.RolloutBucket}").ConfigureAwait(false);

            DalamudVersionInfo versionInfoRelease = JsonConvert.DeserializeObject<DalamudVersionInfo>(versionInfoJsonRelease);

            DalamudVersionInfo? versionInfoStaging = null;

            if (!string.IsNullOrEmpty(settings.DalamudBetaKey))
            {
                var versionInfoJsonStaging = await client.GetAsync(DalamudLauncher.REMOTE_BASE + GetBetaTrackName(settings)).ConfigureAwait(false);

                if (versionInfoJsonStaging.StatusCode != HttpStatusCode.BadRequest)
                    versionInfoStaging = JsonConvert.DeserializeObject<DalamudVersionInfo>(await versionInfoJsonStaging.Content.ReadAsStringAsync().ConfigureAwait(false));
            }

            return (versionInfoRelease, versionInfoStaging);
        }

        private async Task UpdateDalamud()
        {
            var settings = DalamudSettings.GetSettings(this.configDirectory);

            // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var (versionInfoRelease, versionInfoStaging) = await GetVersionInfo(settings).ConfigureAwait(false);

            var remoteVersionInfo = versionInfoRelease;

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

            if (remoteVersionInfo.RuntimeRequired || settings.DoDalamudRuntime)
            {
                Log.Information("[DUPDATE] Now starting for .NET Runtime {0}", remoteVersionInfo.RuntimeVersion);

                var versionFile = new FileInfo(Path.Combine(this.Runtime.FullName, "version"));
                var localVersion = GetLocalRuntimeVersion(versionFile);

                var runtimeNeedsUpdate = localVersion != remoteVersionInfo.RuntimeVersion;

                if (!this.Runtime.Exists)
                    Directory.CreateDirectory(this.Runtime.FullName);

                var isRuntimeIntegrity = false;

                // Only check runtime hashes if we don't need to update it
                if (!runtimeNeedsUpdate)
                {
                    try
                    {
                        isRuntimeIntegrity = await CheckRuntimeHashes(Runtime, localVersion).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DUPDATE] Could not check runtime integrity.");
                    }
                }

                if (runtimePaths.Any(p => !p.Exists) || runtimeNeedsUpdate || !isRuntimeIntegrity)
                {
                    Log.Information("[DUPDATE] Not found, outdated or no integrity: {LocalVer} - {RemoteVer}", localVersion, remoteVersionInfo.RuntimeVersion);

                    SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Runtime);

                    try
                    {
                        Log.Verbose("[DUPDATE] Now download runtime...");
                        await DownloadRuntime(this.Runtime, remoteVersionInfo.RuntimeVersion).ConfigureAwait(false);
                        File.WriteAllText(versionFile.FullName, remoteVersionInfo.RuntimeVersion);
                    }
                    catch (Exception ex)
                    {
                        throw new DalamudIntegrityException("Could not ensure runtime", ex);
                    }
                }
            }

            Log.Verbose("[DUPDATE] Now ensure assets...");

            var assetVer = 0;

            try
            {
                this.SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Assets);
                this.ReportOverlayProgress(null, 0, null);
                var assetResult = await AssetManager.EnsureAssets(this, this.assetDirectory).ConfigureAwait(true);
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

        public static bool IsIntegrity(DirectoryInfo addonPath)
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
                    directory.Delete(true);
                }
                catch
                {
                    // ignored
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
            if (!addonPath.Exists)
                addonPath.Create();
            else
            {
                addonPath.Delete(true);
                addonPath.Create();
            }

            var downloadPath = PlatformHelpers.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            await this.DownloadFile(version.DownloadUrl, downloadPath, this.defaultTimeout).ConfigureAwait(false);
            ZipFile.ExtractToDirectory(downloadPath, addonPath.FullName);

            File.Delete(downloadPath);

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

        private async Task<bool> CheckRuntimeHashes(DirectoryInfo runtimePath, string version)
        {
            var hashesFile = new FileInfo(Path.Combine(runtimePath.FullName, $"hashes-{version}.json"));
            string? runtimeHashes = null;

            if (!hashesFile.Exists)
            {
                Log.Verbose("[DUPDATE] Hashes file does not exist, redownloading...");

                try
                {
                    using var client = new HttpClient();
                    runtimeHashes = await client.GetStringAsync($"https://kamori.goats.dev/Dalamud/Release/Runtime/Hashes/{version}").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DUPDATE] Could not download hashes for runtime v{Version}", version);
                    return false;
                }

                File.WriteAllText(hashesFile.FullName, runtimeHashes);
            }
            else
            {
                runtimeHashes = File.ReadAllText(hashesFile.FullName);
            }

            return CheckIntegrity(runtimePath, runtimeHashes);
        }

        private async Task DownloadRuntime(DirectoryInfo runtimePath, string version)
        {
            // Ensure directory exists
            if (!runtimePath.Exists)
            {
                runtimePath.Create();
            }
            else
            {
                runtimePath.Delete(true);
                runtimePath.Create();
            }

            // Wait for it to be gone, thanks Windows
            Thread.Sleep(1000);

            var dotnetUrl = $"https://kamori.goats.dev/Dalamud/Release/Runtime/DotNet/{version}";
            var desktopUrl = $"https://kamori.goats.dev/Dalamud/Release/Runtime/WindowsDesktop/{version}";

            var downloadPath = PlatformHelpers.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            await this.DownloadFile(dotnetUrl, downloadPath, this.defaultTimeout).ConfigureAwait(false);
            ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName);

            await this.DownloadFile(desktopUrl, downloadPath, this.defaultTimeout).ConfigureAwait(false);
            ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName);

            File.Delete(downloadPath);
        }

        public async Task DownloadFile(string url, string path, TimeSpan timeout)
        {
            if (this.forceProxy && url.Contains("/File/Get/"))
            {
                url = url.Replace("/File/Get/", "/File/GetProxy/");
            }

            using var downloader = new HttpClientDownloadWithProgress(url, path);
            downloader.ProgressChanged += this.ReportOverlayProgress;

            await downloader.Download(timeout).ConfigureAwait(false);
        }
    }

    public class DalamudIntegrityException : Exception
    {
        public DalamudIntegrityException(string msg, Exception? inner = null)
            : base(msg, inner)
        {
        }
    }
}

#nullable restore
