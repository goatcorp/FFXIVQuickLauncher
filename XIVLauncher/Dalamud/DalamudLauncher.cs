using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using Dalamud;
using Dalamud.Discord;
using Dragablz;
using Microsoft.WindowsAPICodePack.Shell.Interop;
using Newtonsoft.Json;
using Serilog;
using Xceed.Wpf.Toolkit.Core.Converters;
using XIVLauncher.Addon;
using XIVLauncher.Game;
using XIVLauncher.Settings;

namespace XIVLauncher.Dalamud
{
    class DalamudLauncher : IPersistentAddon
    {
        private Process _gameProcess;
        private DirectoryInfo _gamePath;
        private ClientLanguage _language;

        public void Setup(Process gameProcess, ILauncherSettingsV3 setting)
        {
            _gameProcess = gameProcess;
            _gamePath = setting.GamePath;
            _language = setting.Language;
        }

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

        public static bool UseDalamudStaging = false;

        private readonly string DALAMUD_MUTEX_NAME = Environment.UserName + "_" + (int.Parse(Util.GetAssemblyVersion().Replace(".", "")) % 0x10 == 0 ? typeof(DalamudLauncher).Name : typeof(DalamudLauncher).Name.Reverse());
        private Mutex _openMutex;

        public void DoWork(object state)
        {
            var cancellationToken = (CancellationToken) state;

            Run(_gamePath, _language, _gameProcess);

            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(1);
            }

            _openMutex?.ReleaseMutex();

            Log.Information("Dalamud mutex closed.");
        }

        private class HooksVersionInfo
        {
            public string AssemblyVersion { get; set; }
            public string SupportedGameVer { get; set; }
        }

        private void Run(DirectoryInfo gamePath, ClientLanguage language, Process gameProcess)
        {
            var addonDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "addon", "Hooks");
            var addonExe = Path.Combine(addonDirectory, "Dalamud.Injector.exe");

            var ingamePluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "installedPlugins");
            var defaultPluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher", "devPlugins");

            Directory.CreateDirectory(ingamePluginPath);
            Directory.CreateDirectory(defaultPluginPath);

            using (var client = new WebClient())
            {
                // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var versionInfoJson = client.DownloadString(Remote + "version");

                var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);


                if (!Mutex.TryOpenExisting(DALAMUD_MUTEX_NAME, out _))
                {
                    _openMutex = new Mutex(true, DALAMUD_MUTEX_NAME);

                    if (!File.Exists(addonExe))
                    {
                        Serilog.Log.Information("[HOOKS] Not found, redownloading");
                        Download(addonDirectory);
                    }
                    else
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(addonExe);
                        var version = versionInfo.ProductVersion;

                        Serilog.Log.Information("Hooks update check: local {0} remote {1}", version,
                            remoteVersionInfo.AssemblyVersion);

                        if (!remoteVersionInfo.AssemblyVersion.StartsWith(version))
                            Download(addonDirectory);
                    }
                }

                if (XivGame.GetLocalGameVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                    return;

                if (!File.Exists(Path.Combine(addonDirectory, "EasyHook.dll")) ||
                    !File.Exists(Path.Combine(addonDirectory, "Dalamud.dll")))
                {
                    MessageBox.Show(
                        "Could not launch the in-game addon successfully. This might be caused by your antivirus.\n To prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".",
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    Directory.Delete(addonDirectory, true);
                    return;
                }

                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "XIVLauncher", "dalamudConfig.json");

                var startInfo = new DalamudStartInfo
                {
                    Language = language,
                    PluginDirectory = ingamePluginPath,
                    DefaultPluginDirectory = defaultPluginPath,
                    ConfigurationPath = configPath,
                    GameVersion = remoteVersionInfo.SupportedGameVer
                };

                var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(startInfo)));

                var process = new Process
                {
                    StartInfo =
                    {
                        FileName = addonExe, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true,
                        Arguments = gameProcess.Id.ToString() + " " + parameters, WorkingDirectory = addonDirectory
                    }
                };

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

        private void Download(string addonPath)
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

            using (var client = new WebClient())
            {
                var downloadPath = Path.GetTempFileName();

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
