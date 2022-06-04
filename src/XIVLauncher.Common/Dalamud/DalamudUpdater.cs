using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
        private readonly DirectoryInfo runtimeDirectory;
        private readonly DirectoryInfo assetDirectory;
        private readonly DirectoryInfo configDirectory;
        private readonly IUniqueIdCache? cache;

        private readonly TimeSpan defaultTimeout = TimeSpan.FromMinutes(15);

        private bool forceProxy = false;

        public DownloadState State { get; private set; } = DownloadState.Unknown;
        public bool IsStaging { get; private set; } = false;

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

        public DirectoryInfo Runtime => this.runtimeDirectory;

        public FileInfo RunnerOverride { get; set; }

        public DirectoryInfo AssetDirectory { get; private set; }

        public IDalamudLoadingOverlay Overlay { get; set; }

        public string RolloutBucket { get; set; }

        public enum DownloadState
        {
            Unknown,
            Done,
            Failed,
            NoIntegrity
        }

        public DalamudUpdater(DirectoryInfo addonDirectory, DirectoryInfo runtimeDirectory, DirectoryInfo assetDirectory, DirectoryInfo configDirectory, IUniqueIdCache? cache, string? dalamudRolloutBucket)
        {
            this.addonDirectory = addonDirectory;
            this.runtimeDirectory = runtimeDirectory;
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
            Overlay.SetStep(progress);
        }

        public void ShowOverlay()
        {
            Overlay.SetVisible();
        }

        public void CloseOverlay()
        {
            Overlay.SetInvisible();
        }

        private void ReportOverlayProgress(long? size, long downloaded, double? progress)
        {
            Overlay.ReportProgress(size, downloaded, progress);
        }

        public void Run()
        {
            Log.Information("[DUPDATE] Starting...");

            Task.Run(async () =>
            {
                const int MAX_TRIES = 10;

                for (var tries = 0; tries < MAX_TRIES; tries++)
                {
                    try
                    {
                        await UpdateDalamud().ConfigureAwait(true);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DUPDATE] Update failed, try {TryCnt}/{MaxTries}...", tries, MAX_TRIES);
                        this.forceProxy = true;
                    }
                }

                if (this.State != DownloadState.Done) this.State = DownloadState.Failed;
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

            if (versionInfoStaging?.Key != null && versionInfoStaging.Key == settings.DalamudBetaKey)
            {
                remoteVersionInfo = versionInfoStaging;
                IsStaging = true;
                Log.Information("[DUPDATE] Using staging version {Kind} with key {Key} ({Hash})", settings.DalamudBetaKind, settings.DalamudBetaKey, remoteVersionInfo.AssemblyVersion);
            }
            else
            {
                Log.Information("[DUPDATE] Using release version ({Hash})", remoteVersionInfo.AssemblyVersion);
            }

            var versionInfoJson = JsonConvert.SerializeObject(remoteVersionInfo);

            var addonPath = new DirectoryInfo(Path.Combine(this.addonDirectory.FullName, "Hooks"));
            var currentVersionPath = new DirectoryInfo(Path.Combine(addonPath.FullName, remoteVersionInfo.AssemblyVersion));
            var runtimePaths = new DirectoryInfo[]
            {
                new(Path.Combine(this.runtimeDirectory.FullName, "host", "fxr", remoteVersionInfo.RuntimeVersion)),
                new(Path.Combine(this.runtimeDirectory.FullName, "shared", "Microsoft.NETCore.App", remoteVersionInfo.RuntimeVersion)),
                new(Path.Combine(this.runtimeDirectory.FullName, "shared", "Microsoft.WindowsDesktop.App", remoteVersionInfo.RuntimeVersion)),
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
                    Log.Error(ex, "[DUPDATE] Could not download dalamud");

                    State = DownloadState.NoIntegrity;
                    return;
                }
            }

            if (remoteVersionInfo.RuntimeRequired || settings.DoDalamudRuntime)
            {
                Log.Information("[DUPDATE] Now starting for .NET Runtime {0}", remoteVersionInfo.RuntimeVersion);

                var versionFile = new FileInfo(Path.Combine(this.runtimeDirectory.FullName, "version"));
                var localVersion = "5.0.6"; // This is the version we first shipped. We didn't write out a version file, so we can't check it.
                if (versionFile.Exists)
                    localVersion = File.ReadAllText(versionFile.FullName);

                if (!this.runtimeDirectory.Exists)
                    Directory.CreateDirectory(this.runtimeDirectory.FullName);

                var integrity = await CheckRuntimeHashes(runtimeDirectory, localVersion).ConfigureAwait(false);

                if (runtimePaths.Any(p => !p.Exists) || localVersion != remoteVersionInfo.RuntimeVersion || !integrity)
                {
                    Log.Information("[DUPDATE] Not found, outdated or no integrity: {LocalVer} - {RemoteVer}", localVersion, remoteVersionInfo.RuntimeVersion);

                    SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Runtime);

                    try
                    {
                        await DownloadRuntime(this.runtimeDirectory, remoteVersionInfo.RuntimeVersion).ConfigureAwait(false);
                        File.WriteAllText(versionFile.FullName, remoteVersionInfo.RuntimeVersion);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DUPDATE] Could not download runtime");

                        State = DownloadState.Failed;
                        return;
                    }
                }
            }

            try
            {
                this.SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Assets);
                this.ReportOverlayProgress(null, 0, null);
                AssetDirectory = await AssetManager.EnsureAssets(this.assetDirectory, this.forceProxy).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] Asset ensurement error, bailing out...");
                State = DownloadState.Failed;
                return;
            }

            if (!IsIntegrity(currentVersionPath))
            {
                Log.Error("[DUPDATE] Integrity check failed after ensurement.");

                State = DownloadState.NoIntegrity;
                return;
            }

            WriteVersionJson(currentVersionPath, versionInfoJson);

            Log.Information("[DUPDATE] All set for " + remoteVersionInfo.SupportedGameVer);

            Runner = new FileInfo(Path.Combine(currentVersionPath.FullName, "Dalamud.Injector.exe"));

            State = DownloadState.Done;
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

                if (!devPath.Exists)
                    devPath.Create();
                else
                {
                    devPath.Delete(true);
                    devPath.Create();
                }

                foreach (var fileInfo in addonPath.GetFiles())
                {
                    fileInfo.CopyTo(Path.Combine(devPath.FullName, fileInfo.Name));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] Could not copy to dev folder.");
            }
        }

        private async Task<bool> CheckRuntimeHashes(DirectoryInfo runtimePath, string version)
        {
            var hashesFile = new FileInfo(Path.Combine(runtimePath.FullName, $"hashes-{version}.json"));
            string? runtimeHashes = null;

            if (!hashesFile.Exists)
            {
                Log.Verbose("Hashes file does not exist, redownloading...");

                using var client = new HttpClient();
                runtimeHashes = await client.GetStringAsync($"https://kamori.goats.dev/Dalamud/Release/Runtime/Hashes/{version}").ConfigureAwait(false);

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

        private async Task DownloadFile(string url, string path, TimeSpan timeout)
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
}