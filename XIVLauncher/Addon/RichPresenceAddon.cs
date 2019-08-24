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

namespace XIVLauncher.Addon
{
    class RichPresenceAddon : IAddon
    {
        private const string Remote = "https://goaaats.github.io/ffxiv/tools/launcher/addons/RichPresence/";


        public void Run(Process gameProcess)
        {
            // RichPresence doesn't work on DX9 and probably never will
            if (!Settings.IsDX11())
                return;

            var addonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "RichPresence");
            var addonExe = Path.Combine(addonDirectory, "FFXIVRichPresenceRunner.exe");

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
                    var version = versionInfo.ProductVersion;

                    if (!remoteVersion.StartsWith(version))
                        Download(addonDirectory);
                }
            }

            // If there's a manual installation of Rich Presence, we shouldn't launch it twice if deletion failed
            if (!CheckManualInstall())
                return;

            var process = new Process
            {
                StartInfo = { FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = gameProcess.Id.ToString() }
            };

            process.Start();
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

        private bool CheckManualInstall()
        {
            try
            {
                // Delete a manually installed version of RichPresence, don't need to launch it twice
                var dump64Path = Path.Combine(Settings.GamePath.FullName, "game", "dump64.dll");
                if (File.Exists(dump64Path))
                    File.Delete(dump64Path);

                return true;
            }
            catch (Exception)
            {
                MessageBox.Show("XIVLauncher found a manual installation of FFXIV Rich Presence, but could not remove it.\nTo fix this, please close any instances of FINAL FANTASY XIV, start XIVLauncher as administrator and log in.");
                return false;
            }
        }

        public string Name => "FFXIV Discord Rich Presence";
    }
}
