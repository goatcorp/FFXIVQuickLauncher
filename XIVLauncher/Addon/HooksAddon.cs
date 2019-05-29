using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace XIVLauncher.Addon
{
    class HooksAddon : IAddon
    {
        private const string Remote = "https://goaaats.github.io/ffxiv/tools/launcher/addons/Hooks/";

        internal class HooksVersionInfo
        {
            public string AssemblyVersion { get; set;  }
            public string SupportedGameVer { get; set; }
        }

        public void Run(Process gameProcess)
        {
            // Launcher Hooks don't work on DX9 and probably never will
            if (!Settings.IsDX11())
                return;

            var addonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "Hooks");
            var addonExe = Path.Combine(addonDirectory, "Dalamud.Injector.exe");

            using (var client = new WebClient())
            {
                var versionInfoJson = client.DownloadString(Remote + "version");
                var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);

                if (!File.Exists(addonExe))
                {
                    Download(addonDirectory);
                }
                else
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(addonExe);
                    var version = versionInfo.ProductVersion;

                    if (!remoteVersionInfo.AssemblyVersion.StartsWith(version))
                        Download(addonDirectory);
                }

                if (XIVGame.GetLocalGamever() != remoteVersionInfo.SupportedGameVer)
                    return;

                var parameters = $" langId={(int) Settings.GetLanguage()} dwhUrl={Settings.GetDiscordWebhookUrl()}";

                if (Settings.IsRmtFilterEnabled())
                    parameters += " rmtFilter";

                if (Settings.IsChatNotificationsEnabled())
                    parameters += " chatNotify";

                if (Settings.IsCfNotificationsEnabled())
                    parameters += " cfNotify";

                var process = new Process
                {
                    StartInfo = { FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = gameProcess.Id.ToString() + parameters, WorkingDirectory = addonDirectory }
                };

                process.Start();
            }
        }

        private void Download(string path)
        {
            // Ensure directory exists
            Directory.CreateDirectory(path);

            var directoryInfo = new DirectoryInfo(path);

            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete(); 
            }

            foreach (var dir in directoryInfo.GetDirectories())
            {
                dir.Delete(true); 
            }

            using (var client = new WebClient())
            {
                var downloadPath = Path.Combine(path, "download.zip");

                if (File.Exists(downloadPath))
                    File.Delete(downloadPath);

                client.DownloadFile(Remote + "latest.zip", downloadPath);
                ZipFile.ExtractToDirectory(downloadPath, path);

                File.Delete(downloadPath);
            }
        }

        public string Name => "XIVLauncher in-game features";
    }
}
