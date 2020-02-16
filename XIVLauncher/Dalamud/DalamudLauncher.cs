using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using Dalamud;
using Dalamud.Discord;
using Microsoft.WindowsAPICodePack.Shell.Interop;
using Newtonsoft.Json;
using XIVLauncher.Dalamud;
using XIVLauncher.Dalamud.PluginUpdate;
using XIVLauncher.Game;

namespace XIVLauncher.Dalamud
{
    class DalamudLauncher
    {
        private const string REMOTE_BASE = "https://goaaats.github.io/ffxiv/tools/launcher/addons/Hooks/";

        private static string Remote
        {
            get
            {
                if (UseDalamudStaging)
                    return REMOTE_BASE + "stg/";

                return REMOTE_BASE;
            }
        }

        private Process _gameProcess;

        public static bool UseDalamudStaging = false;
        
        public DalamudLauncher(Process gameProcess)
        {
            _gameProcess = gameProcess;
        }

        private class HooksVersionInfo
        {
            public string AssemblyVersion { get; set; }
            public string SupportedGameVer { get; set; }
        }

        public void Run(DirectoryInfo gamePath, ClientLanguage language)
        {
            var addonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks");
            var addonExe = Path.Combine(addonDirectory, "Dalamud.Injector.exe");

            var ingamePluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "plugins");
            var defaultPluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "defaultplugins");

            Directory.CreateDirectory(ingamePluginPath);

            using (var client = new WebClient())
            {
                // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var versionInfoJson = client.DownloadString(Remote + "version");

                var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);

                if (!File.Exists(addonExe))
                {
                    Serilog.Log.Information("[HOOKS] Not found, redownloading");
                    Download(addonDirectory, defaultPluginPath);
                }
                else
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(addonExe);
                    var version = versionInfo.ProductVersion;

                    Serilog.Log.Information("Hooks update check: local {0} remote {1}", version, remoteVersionInfo.AssemblyVersion);

                    if (!remoteVersionInfo.AssemblyVersion.StartsWith(version))
                        Download(addonDirectory, defaultPluginPath);
                }

                if (XivGame.GetLocalGameVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                    return;

                if (!File.Exists(Path.Combine(addonDirectory, "EasyHook.dll")))
                {
                    MessageBox.Show(
                        "Could not launch the in-game addon successfully. This might be caused by your antivirus.\n To prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".",
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    Directory.Delete(addonDirectory, true);
                    return;
                }

                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "dalamudConfig.json");

                var startInfo = new DalamudStartInfo
                {
                    Language = language,
                    PluginDirectory = ingamePluginPath,
                    DefaultPluginDirectory = defaultPluginPath,
                    ConfigurationPath = configPath
                };

                var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(startInfo)));

                var process = new Process
                {
                    StartInfo = { FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = _gameProcess.Id.ToString() + " " + parameters, WorkingDirectory = addonDirectory }
                };

                // Update plugins
                PluginUpdateMaster.Run(startInfo.PluginDirectory);

                process.Start();

                Serilog.Log.Information("Started dalamud!");

                // Reset security protocol after updating
                ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
            }
        }

        public static bool CanRunDalamud(DirectoryInfo gamePath)
        {
            using (var client = new WebClient())
            {
                var versionInfoJson = client.DownloadString(Remote + "version");
                var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);


                if (XivGame.GetLocalGameVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                    return false;
            }

            return true;
        }

        private void Download(string addonPath, string ingamePluginPath)
        {
            Serilog.Log.Information("Downloading updates for Hooks and default plugins...");

            // Ensure directory exists
            Directory.CreateDirectory(addonPath);

            var hooksDirectory = new DirectoryInfo(addonPath);

            foreach (var file in hooksDirectory.GetFiles())
            {
                file.Delete(); 
            }

            foreach (var dir in hooksDirectory.GetDirectories())
            {
                dir.Delete(true); 
            }

            var ingamePluginDirectory = new DirectoryInfo(ingamePluginPath);

            if (ingamePluginDirectory.Exists)
            {
                foreach (var file in ingamePluginDirectory.GetFiles())
                {
                    file.Delete();
                }

                foreach (var dir in ingamePluginDirectory.GetDirectories())
                {
                    dir.Delete(true);
                }
            }

            using (var client = new WebClient())
            {
                var downloadPath = Path.Combine(addonPath, "download.zip");

                if (File.Exists(downloadPath))
                    File.Delete(downloadPath);

                client.DownloadFile(Remote + "latest.zip", downloadPath);
                ZipFile.ExtractToDirectory(downloadPath, addonPath);

                File.Delete(downloadPath);
            }

            Thread.Sleep(1000);
        }

        public string Name => "XIVLauncher in-game features";
    }
}
