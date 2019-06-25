using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
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

        private class DalamudConfiguration
        {
            public int LanguageId { get; set; }
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
                "XIVLauncher", "ingameplugins");

            using (var client = new WebClient())
            {
                var versionInfoJson = client.DownloadString(Remote + "version");
                var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);

                if (!File.Exists(addonExe))
                {
                    Download(addonDirectory, ingamePluginPath);
                }
                else
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(addonExe);
                    var version = versionInfo.ProductVersion;

                    if (!remoteVersionInfo.AssemblyVersion.StartsWith(version))
                        Download(addonDirectory, ingamePluginPath);
                }

                if (XIVGame.GetLocalGamever() != remoteVersionInfo.SupportedGameVer)
                    return;

                var dalamudConfig = new DalamudConfiguration
                {
                    LanguageId = (int) Settings.GetLanguage(),
                    DiscordFeatureConfig = Settings.DiscordFeatureConfig
                };

                var parameters = JsonConvert.SerializeObject(dalamudConfig);

                var process = new Process
                {
                    StartInfo = { FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = gameProcess.Id.ToString() + parameters, WorkingDirectory = addonDirectory }
                };

                process.Start();
            }
        }

        private void Download(string addonPath, string ingamePluginPath)
        {
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
