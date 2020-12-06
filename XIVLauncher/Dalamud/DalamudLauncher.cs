using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using CheapLoc;
using Dalamud;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Addon;
using XIVLauncher.Game;
using XIVLauncher.PatchInstaller;
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
            _language = setting.Language.GetValueOrDefault(ClientLanguage.English);
        }

        private const string REMOTE_BASE = "https://goaaats.github.io/ffxiv/tools/launcher/addons/Hooks/";

        private readonly string DALAMUD_MUTEX_NAME = Environment.UserName + "_" + (int.Parse(Util.GetAssemblyVersion().Replace(".", "")) % 0x10 == 0 ? typeof(DalamudLauncher).Name : typeof(DalamudLauncher).Name.Reverse());

        public void DoWork(object state)
        {
            var cancellationToken = (CancellationToken) state;

            var mutex = new Mutex(false, DALAMUD_MUTEX_NAME);
            try
            {
                var isMine = mutex.WaitOne(0, false);

                Run(_gamePath, _language, _gameProcess, isMine);

                while (!cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                }
            }
            finally
            {
                mutex.Close();
                mutex = null;

                Log.Information("Dalamud mutex closed.");
            }
        }

        private class HooksVersionInfo
        {
            public string AssemblyVersion { get; set; }
            public string SupportedGameVer { get; set; }
        }

        private void Run(DirectoryInfo gamePath, ClientLanguage language, Process gameProcess, bool doDownloads)
        {
            if (!CheckVcRedist())
                return;

            var addonDirectory = Path.Combine(Paths.RoamingPath, "addon", "Hooks");
            var addonExe = Path.Combine(addonDirectory, "Dalamud.Injector.exe");

            var ingamePluginPath = Path.Combine(Paths.RoamingPath, "installedPlugins");
            var defaultPluginPath = Path.Combine(Paths.RoamingPath, "devPlugins");

            Directory.CreateDirectory(ingamePluginPath);
            Directory.CreateDirectory(defaultPluginPath);

            var configPath = Path.Combine(Paths.RoamingPath, "dalamudConfig.json");
            var config = DalamudSettings.DalamudConfig;

            Thread.Sleep((int) App.Settings.DalamudInjectionDelayMs);

            using var client = new WebClient();

            // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var versionInfoJson = client.DownloadString(REMOTE_BASE + (config.DoDalamudTest ? "stg/" : string.Empty) + "version");

            var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);


            if (doDownloads)
            {
                if (!File.Exists(addonExe))
                {
                    Serilog.Log.Information("[HOOKS] Not found, redownloading");
                    Download(addonDirectory, config.DoDalamudTest);
                }
                else
                {
                    var versionInfo = FileVersionInfo.GetVersionInfo(addonExe);
                    var version = versionInfo.ProductVersion;

                    Serilog.Log.Information("[HOOKS] Hooks update check: local {0} remote {1}", version,
                        remoteVersionInfo.AssemblyVersion);

                    if (!remoteVersionInfo.AssemblyVersion.StartsWith(version))
                        Download(addonDirectory, config.DoDalamudTest);
                }
            }

            if (Repository.Ffxiv.GetVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                return;

            if (!File.Exists(Path.Combine(addonDirectory, "EasyHook.dll")) ||
                !File.Exists(Path.Combine(addonDirectory, "Dalamud.dll")) ||
                !File.Exists(Path.Combine(addonDirectory, "Dalamud.Injector.exe")))
            {
                MessageBox.Show(
                    "Could not launch the in-game addon successfully. This might be caused by your antivirus.\n To prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".",
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                Directory.Delete(addonDirectory, true);
                return;
            }

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

            Serilog.Log.Information("[HOOKS] Started dalamud! Staging: " + config.DoDalamudTest);

            // Reset security protocol after updating
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }

        public static bool CanRunDalamud(DirectoryInfo gamePath)
        {
            using var client = new WebClient();

            var versionInfoJson = client.DownloadString(REMOTE_BASE + "version");
            var remoteVersionInfo = JsonConvert.DeserializeObject<HooksVersionInfo>(versionInfoJson);


            if (Repository.Ffxiv.GetVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                return false;

            return true;
        }

        private void Download(string addonPath, bool staging)
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

                client.DownloadFile(REMOTE_BASE + (staging ? "stg/" : string.Empty) + "latest.zip", downloadPath);
                ZipFile.ExtractToDirectory(downloadPath, addonPath);

                File.Delete(downloadPath);
            }

            Thread.Sleep(1000);
        }

        private static bool CheckVcRedist()
        {
            if (CheckDotNet48() && CheckVc2019() &&
                File.Exists(Environment.ExpandEnvironmentVariables("%SystemRoot%\\System32\\vcruntime140_clr0400.dll")))
            {
                return true;
            }
            else if (!CheckDotNet48() && CheckVc2019())
            {
                var res = MessageBox.Show(
                Loc.Localize("DalamudDotNet48RedistError",
                    "The XIVLauncher in-game addon needs the .NET Framework 4.8 to be installed to continue. Please install it from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return false;
            }
            else if (CheckDotNet48() && !CheckVc2019())
            {
                var res = MessageBox.Show(
                Loc.Localize("DalamudVc2019RedistError",
                    "The XIVLauncher in-game addon needs the Microsoft Visual C++ 2015-2019 redistributable to be installed to continue. Please install it from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return false;
            }
            else
            {
                var res = MessageBox.Show(
                Loc.Localize("DalamudVcRedistError",
                    "The XIVLauncher in-game addon needs the Microsoft Visual C++ 2015 redistributable and .NET Framework 4.8 to be installed to continue. Please install them from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return false;
            }
        }

        private static bool CheckDotNet48()
        {
            Serilog.Log.Information("Checking for .Net 4.8 or later...");

            // copied and adjusted from https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed

            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

            using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
            {
                if (ndpKey != null && ndpKey.GetValue("Release") != null && (int)ndpKey.GetValue("Release") >= 528040)
                {
                    return true;
                }
                else
                {
                    Serilog.Log.Error(".Net 4.8 or later not found");
                    return false;
                }
            }

            
        }

        private static bool CheckVc2019()
        {
            Serilog.Log.Information("Checking for VS 2015-2019 Redist...");

            // snipped from https://stackoverflow.com/questions/12206314/detect-if-visual-c-redistributable-for-visual-studio-2012-is-installed
            // and https://github.com/bitbeans/RedistributableChecker

            var vcreg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DevDiv\VC\Servicing\14.0\RuntimeMinimum", false);
            if (vcreg == null) return false;
            var vcVersion = vcreg.GetValue("Version");
            if (((string)vcVersion).StartsWith("14"))
            {
                return true;
            }

            Serilog.Log.Error("VC 2015-2019 redistributable not found");
            return false;
        }


        public string Name => "XIVLauncher in-game features";
    }
}
