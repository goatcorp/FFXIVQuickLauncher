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

namespace XIVLauncher.Common.Dalamud
{
    public class DalamudUpdater
    {
        private readonly DirectoryInfo addonDirectory;
        private readonly DirectoryInfo runtimeDirectory;
        private readonly DirectoryInfo assetDirectory;
        private readonly IUniqueIdCache? cache;
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

        public FileInfo RunnerOverride { get; set; }

        public DirectoryInfo AssetDirectory { get; private set; }

        public IDalamudLoadingOverlay Overlay { get; set; }

        public enum DownloadState
        {
            Unknown,
            Done,
            Failed,
            NoIntegrity
        }

        public DalamudUpdater(DirectoryInfo addonDirectory, DirectoryInfo runtimeDirectory, DirectoryInfo assetDirectory, IUniqueIdCache? cache)
        {
            this.addonDirectory = addonDirectory;
            this.runtimeDirectory = runtimeDirectory;
            this.assetDirectory = assetDirectory;
            this.cache = cache;
        }

        public void SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep progress)
        {
            //Overlay?.Dispatcher.Invoke(() => Overlay.SetProgress(progress));
            Overlay.SetStep(progress);
        }

        public void ShowOverlay()
        {
            //Overlay?.Dispatcher.Invoke(() => Overlay.SetVisible());
            Overlay.SetVisible();
        }

        public void CloseOverlay()
        {
            //Overlay?.Dispatcher.Invoke(() => Overlay.Close());
            Overlay.SetInvisible();
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
                    }
                }

                if (this.State != DownloadState.Done) this.State = DownloadState.Failed;
            });
        }

        private static string GetBetaTrackName(DalamudSettings settings) =>
            string.IsNullOrEmpty(settings.DalamudBetaKind) ? "staging" : settings.DalamudBetaKind;

        private static async Task<(DalamudVersionInfo release, DalamudVersionInfo? staging)> GetVersionInfo(DalamudSettings settings)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5),
            };

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
            };

            var versionInfoJsonRelease = await client.GetStringAsync(DalamudLauncher.REMOTE_BASE + "release").ConfigureAwait(false);

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
            var settings = DalamudSettings.GetSettings();

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
                    await Download(currentVersionPath, settings, remoteVersionInfo).ConfigureAwait(true);
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

                if (runtimePaths.Any(p => !p.Exists) || localVersion != remoteVersionInfo.RuntimeVersion)
                {
                    Log.Information("[DUPDATE] Not found or outdated: {LocalVer} - {RemoteVer}", localVersion, remoteVersionInfo.RuntimeVersion);

                    SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Runtime);

                    try
                    {
                        await DownloadRuntime(this.runtimeDirectory, remoteVersionInfo.RuntimeVersion);
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
                SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Assets);
                AssetDirectory = await AssetManager.EnsureAssets(assetDirectory).ConfigureAwait(true);
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

                var hashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(hashesPath));

                foreach (var hash in hashes)
                {
                    var file = Path.Combine(addonPath.FullName, hash.Key.Replace("\\", "/"));
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
                Log.Error(ex, "[DUPDATE] No dalamud integrity");
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

        private static async Task Download(DirectoryInfo addonPath, DalamudSettings settings, DalamudVersionInfo version)
        {
            // Ensure directory exists
            if (!addonPath.Exists)
                addonPath.Create();
            else
            {
                addonPath.Delete(true);
                addonPath.Create();
            }

            var downloadPath = Util.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(25),
            };

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
            };

            var bytes = await client.GetByteArrayAsync(version.DownloadUrl).ConfigureAwait(true);
            File.WriteAllBytes(downloadPath, bytes);

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

        private static async Task DownloadRuntime(DirectoryInfo runtimePath, string version)
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

            var dotnetUrl = $"https://dotnetcli.azureedge.net/dotnet/Runtime/{version}/dotnet-runtime-{version}-win-x64.zip";
            var desktopUrl = $"https://dotnetcli.azureedge.net/dotnet/WindowsDesktop/{version}/windowsdesktop-runtime-{version}-win-x64.zip";

            var downloadPath = Util.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(25),
            };

            var bytesDn = await client.GetByteArrayAsync(dotnetUrl);
            File.WriteAllBytes(downloadPath, bytesDn);
            ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName);

            var bytesDesktop = await client.GetByteArrayAsync(desktopUrl);
            File.WriteAllBytes(downloadPath, bytesDesktop);
            ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName);

            File.Delete(downloadPath);
        }
    }
}