using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using Dalamud.Discord;
using Newtonsoft.Json;

namespace XIVLauncher.Addon
{
    class HooksAddon : IAddon
    {
        private const string Remote = "https://goaaats.github.io/ffxiv/tools/launcher/addons/Hooks/";

        private class HooksVersionInfo
        {
            public string AssemblyVersion { get; set;  }
            public string SupportedGameVer { get; set; }
        }

        [Serializable]
        public sealed class DalamudStartInfo {
            public string WorkingDirectory;
            public string PluginDirectory;
            public string DefaultPluginDirectory;
            public int LanguageId;

            public DiscordFeatureConfiguration DiscordFeatureConfig { get; set; }
        }

        public void Run(Process gameProcess)
        {
            // Launcher Hooks don't work on DX9 and probably never will
            if (!Settings.IsDX11())
                return;

            var addonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks");
            var addonExe = Path.Combine(addonDirectory, "Dalamud.Injector.exe");

            var ingamePluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "plugins");
            var defaultPluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "defaultplugins");

            Directory.CreateDirectory(ingamePluginPath);

            using (var client = new WebClient())
            {
                var versionInfoJson = client.DownloadString(Remote + "version");
                var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);

                if (!File.Exists(addonExe))
                {
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

                if (XIVGame.GetLocalGamever() != remoteVersionInfo.SupportedGameVer)
                    return;

                var dalamudConfig = new DalamudStartInfo
                {
                    LanguageId = (int) Settings.GetLanguage(),
                    DiscordFeatureConfig = Settings.DiscordFeatureConfig,
                    PluginDirectory = ingamePluginPath,
                    DefaultPluginDirectory = defaultPluginPath
                };

                var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dalamudConfig)));

                var process = new Process
                {
                    StartInfo = { FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = gameProcess.Id.ToString() + " " + parameters, WorkingDirectory = addonDirectory }
                };

                Serilog.Log.Information("Starting dalamud with parameters: {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

                process.Start();
            }
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

            Directory.CreateDirectory(ingamePluginPath);

            var ingamePluginDirectory = new DirectoryInfo(ingamePluginPath);

            foreach (var file in ingamePluginDirectory.GetFiles())
            {
                file.Delete(); 
            }

            foreach (var dir in ingamePluginDirectory.GetDirectories())
            {
                dir.Delete(true); 
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

            using (var client = new WebClient())
            {
                var downloadPath = Path.Combine(ingamePluginPath, "plugins.zip");

                if (File.Exists(downloadPath))
                    File.Delete(downloadPath);

                client.DownloadFile(Remote + "plugins.zip", downloadPath);
                ZipFile.ExtractToDirectory(downloadPath, ingamePluginPath);

                File.Delete(downloadPath);
            }
        }

        public string Name => "XIVLauncher in-game features";
    }
}
