using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Addon
{
    class RichPresenceAddon : IAddon
    {
        private const string Remote = "https://goaaats.github.io/ffxiv/tools/launcher/addons/RichPresence/";


        public void Run()
        {
            var addonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "RichPresence");
            var addonExe = Path.Combine(addonDirectory, "FFXIVRichPresenceRunner.exe");

            // Ensure directory exists
            Directory.CreateDirectory(addonDirectory);

            if (!File.Exists(addonExe))
            {
                Download(addonDirectory);
            }
            else
            {
                using (var client = new WebClient())
                {
                    var remoteVersion = client.DownloadString(Remote + "version");

                    var versionInfo = FileVersionInfo.GetVersionInfo(addonExe);
                    string version = versionInfo.ProductVersion;

                    if(!remoteVersion.StartsWith(version))
                        Download(addonDirectory);
                }
            }

            var process = new Process
            {
                StartInfo = {FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true}
            };

            process.Start();
        }

        private void Download(string path)
        {
            using (var client = new WebClient())
            {
                var downloadPath = Path.Combine(path, "download.zip");

                client.DownloadFile(Remote + "latest.zip", downloadPath);
                ZipFile.ExtractToDirectory(downloadPath, path);

                File.Delete(downloadPath);
            }

            // Delete a manually installed version of RichPresence, don't need to launch it twice
            var dump64path = Path.Combine(Settings.GetGamePath(), "game", "dump64.dll");
            if (File.Exists(dump64path))
                File.Delete(dump64path);
        }

        public string Name => "FFXIV Discord Rich Presence";
    }
}
