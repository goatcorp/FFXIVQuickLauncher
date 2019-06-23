using FFXIVOtpLinker;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Threading.Tasks;

namespace XIVLauncher.Addon
{
    class OTPLinkAddon : MarshalByRefObject, IServiceAddon
    {
        private const string Remote = "https://roy-n-roy.github.io/FFXIVOtpLinker/";

        public string Name => "FFXIV Onetime Password Linkage Server";
        private const int httpPort = 1050;
        public void Run(MainWindow window)
        {
            var addonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "addon", "OtpLink");
            var addonExe = Path.Combine(addonDirectory, "FFXIVOtpLinkServer.exe");

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


            OtpServer otpServer;
            ChannelServices.RegisterChannel(new IpcServerChannel("otpLink"), false);
            RemotingServices.Marshal(otpServer = new OtpServer
            {
                LoginAction = (string otpText) =>
                {
                    window.HandleLogin(false, otpText);
                }
            }, "launcher", typeof(OtpServer));

            otpServer.Process = new Process
            {
                StartInfo = { FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = httpPort.ToString() + " Login" }
            };

            otpServer.Process.Start();

        }

        public void Run(Process _)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            OtpServer otpServer = null;
            try
            {
                ChannelServices.RegisterChannel(new IpcClientChannel(), false);
                otpServer = Activator.GetObject(typeof(OtpServer), "ipc://otpLink/launcher") as OtpServer;

                otpServer?.Process?.Kill();
            }
            catch (Exception ex)
            {
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
    }
}
