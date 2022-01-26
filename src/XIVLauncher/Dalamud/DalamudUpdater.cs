using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet;
using Serilog;
using XIVLauncher.Cache;
using XIVLauncher.Settings;
using XIVLauncher.Windows;

namespace XIVLauncher.Dalamud
{
    static class DalamudUpdater
    {
        public static DownloadState State { get; private set; } = DownloadState.Unknown;
        public static bool IsStaging { get; private set; } = false;

        private static FileInfo _runnerInternal;
        public static FileInfo Runner
        {
            get
            {
                if (RunnerOverride != null)
                    return RunnerOverride;

                return _runnerInternal;
            }
            private set => _runnerInternal = value;
        }

        public static FileInfo RunnerOverride { get; set; }

        public static DirectoryInfo AssetDirectory { get; private set; }

        public static DalamudLoadingOverlay Overlay { get; set; }

        public enum DownloadState
        {
            Unknown,
            Done,
            Failed,
            NoIntegrity
        }

        public static void SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress progress)
        {
            Overlay?.Dispatcher.Invoke(() => Overlay.SetProgress(progress));
        }

        public static void ShowOverlay()
        {
            Overlay?.Dispatcher.Invoke(() => Overlay.SetVisible());
        }

        public static void CloseOverlay()
        {
            Overlay?.Dispatcher.Invoke(() => Overlay.Close());
        }

        public static void Run()
        {
            Log.Information("[DUPDATE] Starting...");

            Task.Run(() =>
            {
                try
                {
                    UpdateDalamud();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DUPDATE] Update failed...");
                    State = DownloadState.Failed;
                }
            });
        }

        private static string GetBetaPath(DalamudSettings settings) =>
            string.IsNullOrEmpty(settings.DalamudBetaKind) ? "stg/" : $"{settings.DalamudBetaKind}/";

        private static (DalamudVersionInfo release, DalamudVersionInfo staging) GetVersionInfo(WebClient client, DalamudSettings settings)
        {
            var versionInfoJsonRelease = client.DownloadString(DalamudLauncher.REMOTE_BASE + "version");
            var versionInfoJsonStaging = client.DownloadString(DalamudLauncher.REMOTE_BASE + GetBetaPath(settings) + "version");

            var versionInfoRelease = JsonConvert.DeserializeObject<DalamudVersionInfo>(versionInfoJsonRelease);
            var versionInfoStaging = JsonConvert.DeserializeObject<DalamudVersionInfo>(versionInfoJsonStaging);

            return (versionInfoRelease, versionInfoStaging);
        }

        private static void UpdateDalamud()
        {
            using var client = new WebClient();

            var settings = DalamudSettings.GetSettings();

            // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var (versionInfoRelease, versionInfoStaging) = GetVersionInfo(client, settings);

            var remoteVersionInfo = versionInfoRelease;
            if (versionInfoStaging.Key == settings.DalamudBetaKey)
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

            var addonPath = new DirectoryInfo(Path.Combine(Paths.RoamingPath, "addon", "Hooks"));
            var currentVersionPath = new DirectoryInfo(Path.Combine(addonPath.FullName, remoteVersionInfo.AssemblyVersion));
            var runtimePath = new DirectoryInfo(Path.Combine(Paths.RoamingPath, "runtime"));
            var runtimePaths = new DirectoryInfo[]
            {
                new(Path.Combine(runtimePath.FullName, "host", "fxr", remoteVersionInfo.RuntimeVersion)),
                new(Path.Combine(runtimePath.FullName, "shared", "Microsoft.NETCore.App", remoteVersionInfo.RuntimeVersion)),
                new(Path.Combine(runtimePath.FullName, "shared", "Microsoft.WindowsDesktop.App", remoteVersionInfo.RuntimeVersion)),
            };

            if (!currentVersionPath.Exists || !IsIntegrity(currentVersionPath))
            {
                Log.Information("[DUPDATE] Not found, redownloading");

                SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Dalamud);

                try
                {
                    Download(currentVersionPath, settings, IsStaging);
                    CleanUpOld(addonPath, remoteVersionInfo.AssemblyVersion);

                    // This is a good indicator that we should clear the UID cache
                    UniqueIdCache.Instance.Reset();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DUPDATE] Could not download update package.");

                    State = DownloadState.NoIntegrity;
                    return;
                }

                Log.Information("[DUPDATE] Download OK!");
            }

            if (remoteVersionInfo.RuntimeRequired || settings.DoDalamudRuntime)
            {
                Log.Information("[DUPDATE] Now starting for .NET Runtime {0}", remoteVersionInfo.RuntimeVersion);

                if (runtimePaths.Any(p => !p.Exists))
                {
                    Log.Information("[DUPDATE] Not found, redownloading");

                    SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Runtime);

                    try
                    {
                        DownloadRuntime(runtimePath, remoteVersionInfo.RuntimeVersion);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DUPDATE] Could not download update package.");

                        State = DownloadState.Failed;
                        return;
                    }

                    Log.Information("[DUPDATE] Download OK!");
                }
            }

            try
            {
                SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Assets);

                var assetBase = new DirectoryInfo(Path.Combine(Paths.RoamingPath, "dalamudAssets"));
                if (!AssetManager.TryEnsureAssets(assetBase, out var assetsDir))
                {
                    Log.Information("[DUPDATE] Assets not ensured, bailing out...");
                    State = DownloadState.Failed;

                    // TODO(goat): We might want to try again here
                    try
                    {
                        assetBase.Delete(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Tried to recover assets, didn't work");
                    }
                    return;
                }

                AssetDirectory = assetsDir;
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

        public static bool IsIntegrity(DirectoryInfo addonPath)
        {
            var files = addonPath.GetFiles();

            try
            {
                files.First(x => x.Name == "Dalamud.Injector.exe").OpenRead().ReadAllBytes();
                files.First(x => x.Name == "Dalamud.dll").OpenRead().ReadAllBytes();
                files.First(x => x.Name == "ImGuiScene.dll").OpenRead().ReadAllBytes();

                var hashesPath = Path.Combine(addonPath.FullName, "hashes.json");
                if (!File.Exists(hashesPath))
                {
                    Log.Error("[DUPDATE] No hashes.json");
                    return false;
                }

                var hashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(hashesPath));
                foreach (var hash in hashes)
                {
                    var file = Path.Combine(addonPath.FullName, hash.Key);
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

        private static void Download(DirectoryInfo addonPath, DalamudSettings settings, bool isStaging)
        {
            // Ensure directory exists
            if (!addonPath.Exists)
                addonPath.Create();
            else
            {
                addonPath.Delete(true);
                addonPath.Create();
            }

            using var client = new WebClient();

            var downloadPath = Util.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            client.DownloadFile(DalamudLauncher.REMOTE_BASE + (isStaging ? GetBetaPath(settings) : string.Empty) + "latest.zip", downloadPath);
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

        private static void DownloadRuntime(DirectoryInfo runtimePath, string version)
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

            using var client = new WebClient();

            var dotnetUrl = $"https://dotnetcli.azureedge.net/dotnet/Runtime/{version}/dotnet-runtime-{version}-win-x64.zip";
            var desktopUrl = $"https://dotnetcli.azureedge.net/dotnet/WindowsDesktop/{version}/windowsdesktop-runtime-{version}-win-x64.zip";

            var downloadPath = Util.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            client.DownloadFile(dotnetUrl, downloadPath);
            ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName);

            client.DownloadFile(desktopUrl, downloadPath);
            ZipFile.ExtractToDirectory(downloadPath, runtimePath.FullName);

            File.Delete(downloadPath);
        }
    }
}