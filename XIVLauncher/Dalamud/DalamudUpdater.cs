using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using Newtonsoft.Json;
using NuGet;
using Serilog;
using XIVLauncher.PatchInstaller;
using XIVLauncher.Settings;
using XIVLauncher.Windows;

namespace XIVLauncher.Dalamud
{
    static class DalamudUpdater
    {
        public static DownloadState State { get; private set; } = DownloadState.Unknown;

        public static FileInfo Runner { get; private set; }
        public static DirectoryInfo AssetDirectory { get; private set; }

        private static DalamudLoadingOverlay overlay;

        public enum DownloadState
        {
            Unknown,
            DownloadDalamud,
            DownloadAssets,
            Done,
            Failed,
            Unavailable,
            NoIntegrity
        }

        public static void SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress progress)
        {
            overlay.Dispatcher.Invoke(() => overlay.SetProgress(progress));
        }

        public static void ShowOverlay()
        {
            overlay.Dispatcher.Invoke(() => overlay.SetVisible());
        }

        public static void CloseOverlay()
        {
            overlay.Dispatcher.Invoke(() => overlay.Close());
        }

        public static void Run(DirectoryInfo gamePath, DalamudLoadingOverlay overlay)
        {
            Log.Information("[DUPDATE] Starting...");

            DalamudUpdater.overlay = overlay;

            Task.Run(() =>
            {
                try
                {
                    UpdateDalamud(gamePath, overlay);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DUPDATE] Update failed...");
                    State = DownloadState.Failed;
                }
            });
        }

        private static void UpdateDalamud(DirectoryInfo gamePath, DalamudLoadingOverlay overlay)
        {
            using var client = new WebClient();

            var doDalamudTest = DalamudSettings.GetSettings().DoDalamudTest;

            // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var versionInfoJson = client.DownloadString(DalamudLauncher.REMOTE_BASE + (doDalamudTest ? "stg/" : string.Empty) + "version");

            var remoteVersionInfo = JsonConvert.DeserializeObject<DalamudVersionInfo>(versionInfoJson);

            var addonPath = new DirectoryInfo(Path.Combine(Paths.RoamingPath, "addon", "Hooks", remoteVersionInfo.AssemblyVersion));
            AssetDirectory = new DirectoryInfo(Path.Combine(Util.GetRoaming(), "dalamudAssets"));

            try
            {
                if (Repository.Ffxiv.GetVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                {
                    State = DownloadState.Unavailable;
                    SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Unavailable);
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] Could not get game version");
                State = DownloadState.Failed;
            }

            if (!addonPath.Exists || !IsIntegrity(addonPath))
            {
                Log.Information("[DUPDATE] Not found, redownloading");

                SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Dalamud);

                try
                {
                    Download(addonPath, doDalamudTest, versionInfoJson);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DUPDATE] Could not download update package.");

                    State = DownloadState.NoIntegrity;
                    return;
                }

                Log.Information("[DUPDATE] Download OK!");
            }

            try
            {
                SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Assets);

                if (!AssetManager.EnsureAssets(AssetDirectory))
                {
                    Log.Information("[DUPDATE] Assets not ensured, bailing out...");
                    State = DownloadState.Failed;
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] Asset ensurement error, bailing out...");
                State = DownloadState.Failed;
                return;
            }

            if (!IsIntegrity(addonPath))
            {
                Log.Error("[DUPDATE] Integrity check failed.");

                State = DownloadState.NoIntegrity;
                return;
            }

            Log.Information("[DUPDATE] All set.");

            Runner = new FileInfo(Path.Combine(addonPath.FullName, "Dalamud.Injector.exe"));

            State = DownloadState.Done;
        }

        public static bool IsIntegrity(DirectoryInfo addonPath)
        {
            var files = addonPath.GetFiles();

            try
            {
                files.First(x => x.Name == "Dalamud.Injector.exe").OpenRead().ReadAllBytes();
                files.First(x => x.Name == "Dalamud.dll").OpenRead().ReadAllBytes();
                files.First(x => x.Name == "CheapLoc.dll").OpenRead().ReadAllBytes();
                files.First(x => x.Name == "ImGuiScene.dll").OpenRead().ReadAllBytes();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DUPDATE] No dalamud integrity.");
                return false;
            }
            
            return true;
        }

        private static void Download(DirectoryInfo addonPath, bool staging, string versionInfoJson)
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

            var downloadPath = Path.GetTempFileName();

            if (File.Exists(downloadPath))
                File.Delete(downloadPath);

            File.WriteAllText(Path.Combine(addonPath.FullName, "version.json"), versionInfoJson);

            client.DownloadFile(DalamudLauncher.REMOTE_BASE + (staging ? "stg/" : string.Empty) + "latest.zip", downloadPath);
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
    }
}
