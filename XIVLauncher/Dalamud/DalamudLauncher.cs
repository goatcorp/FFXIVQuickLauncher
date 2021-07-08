using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
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
using XIVLauncher.Windows;

namespace XIVLauncher.Dalamud
{
    class DalamudLauncher : IRunnableAddon
    {
        private readonly DalamudLoadingOverlay _overlay;
        private Process _gameProcess;
        private DirectoryInfo _gamePath;
        private ClientLanguage _language;
        private bool _optOutMbCollection;

        public DalamudLauncher(DalamudLoadingOverlay overlay)
        {
            _overlay = overlay;
        }

        public void Setup(Process gameProcess, ILauncherSettingsV3 setting)
        {
            _gameProcess = gameProcess;
            _gamePath = setting.GamePath;
            _language = setting.Language.GetValueOrDefault(ClientLanguage.English);
            _optOutMbCollection = setting.OptOutMbCollection.GetValueOrDefault(); ;
        }

        public void Run()
        {
            Run(_gamePath, _language, _gameProcess);
        }

        public const string REMOTE_BASE = "https://goatcorp.github.io/dalamud-distrib/";

        private void Run(DirectoryInfo gamePath, ClientLanguage language, Process gameProcess)
        {
            Log.Information("[HOOKS] DalamudLauncher::Run(gp:{0}, cl:{1}, d:{2}", gamePath.FullName, language);

            if (!CheckVcRedist())
                return;

            var ingamePluginPath = Path.Combine(Paths.RoamingPath, "installedPlugins");
            var defaultPluginPath = Path.Combine(Paths.RoamingPath, "devPlugins");

            Directory.CreateDirectory(ingamePluginPath);
            Directory.CreateDirectory(defaultPluginPath);


            Thread.Sleep((int) App.Settings.DalamudInjectionDelayMs);

            if (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
                DalamudUpdater.ShowOverlay();

            while (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
            {
                if (DalamudUpdater.State == DalamudUpdater.DownloadState.Failed)
                {
                    DalamudUpdater.CloseOverlay();
                    return;
                }

                if (DalamudUpdater.State == DalamudUpdater.DownloadState.NoIntegrity)
                {
                    DalamudUpdater.CloseOverlay();

                    MessageBox.Show(Loc.Localize("DalamudAntivirusHint",
                        "The in-game addon ran into an error.\n\nThis is most likely caused by your antivirus. Please whitelist the quarantined files or disable the in-game addon."));
                    return;
                }

                Thread.Yield();
            }

            if (!DalamudUpdater.Runner.Exists)
            {
                CustomMessageBox.Show(
                    "Could not launch the in-game addon successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".",
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!ReCheckVersion(gamePath))
            {
                DalamudUpdater.SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Unavailable);
                DalamudUpdater.ShowOverlay();
                Log.Error("[HOOKS] ReCheckVersion fail.");

                return;
            }

            var startInfo = new DalamudStartInfo
            {
                Language = language,
                PluginDirectory = ingamePluginPath,
                DefaultPluginDirectory = defaultPluginPath,
                ConfigurationPath = DalamudSettings.ConfigPath,
                AssetDirectory = DalamudUpdater.AssetDirectory.FullName,
                GameVersion = Repository.Ffxiv.GetVer(gamePath),
                OptOutMbCollection = _optOutMbCollection
            };

            var parameters = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(startInfo)));

            var process = new Process
            {
                StartInfo =
                {
                    FileName = DalamudUpdater.Runner.FullName, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true,
                    Arguments = gameProcess.Id.ToString() + " " + parameters, WorkingDirectory = DalamudUpdater.Runner.DirectoryName
                }
            };

            process.Start();

            _overlay.Dispatcher.Invoke(() =>
            {
                _overlay.Hide();
                _overlay.Close();
            });

            DalamudUpdater.CloseOverlay();

            Serilog.Log.Information("[HOOKS] Started dalamud!");

            // Reset security protocol after updating
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }

        private static bool ReCheckVersion(DirectoryInfo gamePath)
        {
            if (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
                return false;

            var info = DalamudVersionInfo.Load(new FileInfo(Path.Combine(DalamudUpdater.Runner.DirectoryName,
                "version.json")));

            if (Repository.Ffxiv.GetVer(gamePath) != info.SupportedGameVer)
                return false;

            return true;
        }

        public static bool CanRunDalamud(DirectoryInfo gamePath)
        {
            using var client = new WebClient();

            var versionInfoJson = client.DownloadString(REMOTE_BASE + "version");
            var remoteVersionInfo = JsonConvert.DeserializeObject<DalamudVersionInfo>(versionInfoJson);


            if (Repository.Ffxiv.GetVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                return false;

            return true;
        }

        private static bool CheckVcRedist()
        {
            // we only need to run these once.
            var checkForDotNet48 = CheckDotNet48();
            var checkForVc2019 = CheckVc2019(); // this also checks all the dll locations now

            if (checkForDotNet48 && checkForVc2019)
            {
                return true;
            }
            else if (!checkForDotNet48 && checkForVc2019)
            {
                Log.Error(".Net 4.8 or later not found");

                CustomMessageBox.Show(
                Loc.Localize("DalamudDotNet48RedistError",
                    "The XIVLauncher in-game addon needs the .NET Framework 4.8 to be installed to continue. Please install it from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return false;
            }
            else if (checkForDotNet48 && !checkForVc2019)
            {
                Log.Error("VC 2015-2019 redistributable not found");

                CustomMessageBox.Show(
                Loc.Localize("DalamudVc2019RedistError",
                    "The XIVLauncher in-game addon needs the Microsoft Visual C++ 2015-2019 redistributable to be installed to continue. Please install it from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return false;
            }
            else
            {
                Log.Error(".Net 4.8 or later not found");
                Log.Error("VC 2015-2019 redistributable not found");

                CustomMessageBox.Show(
                Loc.Localize("DalamudVcRedistError",
                    "The XIVLauncher in-game addon needs the Microsoft Visual C++ 2015-2019 redistributable and .NET Framework 4.8 to be installed to continue. Please install them from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

                return false;
            }
        }

        private static bool CheckDotNet48()
        {
            // copied and adjusted from https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed

            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

            using var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey);

            if (ndpKey?.GetValue("Release") != null && (int)ndpKey.GetValue("Release") >= 528040)
            {
                return true;
            }
            else return false;
        }

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpFileName);

        private static bool CheckLibrary(string fileName)
        {
            return LoadLibrary(fileName) != IntPtr.Zero;
        }

        private static bool CheckVc2019()
        {
            // snipped from https://stackoverflow.com/questions/12206314/detect-if-visual-c-redistributable-for-visual-studio-2012-is-installed
            // and https://github.com/bitbeans/RedistributableChecker

            var vcreg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DevDiv\VC\Servicing\14.0\RuntimeMinimum", false);
            if (vcreg == null) return false;
            var vcVersion = vcreg.GetValue("Version");
            if (((string)vcVersion).StartsWith("14"))
            {
                if (!EnvironmentSettings.IsWine)
                {
                    if (CheckLibrary("ucrtbase_clr0400") &&
                        CheckLibrary("vcruntime140_clr0400") &&
                        CheckLibrary("vcruntime140"))
                        return true;
                }
                else return true;
            }
            return false;
        }


        public string Name => "XIVLauncher in-game features";
    }
}
