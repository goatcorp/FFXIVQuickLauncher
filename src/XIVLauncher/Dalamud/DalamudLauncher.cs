using System;
using System.Diagnostics;
using System.IO;
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
        private readonly DalamudLoadMethod _loadMethod;
        private Process _gameProcess;
        private DirectoryInfo _gamePath;
        private ClientLanguage _language;
        private bool _optOutMbCollection;

        public DalamudLauncher(DalamudLoadingOverlay overlay, DalamudLoadMethod loadMethod)
        {
            _overlay = overlay;
            _loadMethod = loadMethod;
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

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        [DllImport("Dalamud.Boot.dll")]
        static extern int RewriteRemoteEntryPointW(IntPtr hProcess, [MarshalAs(UnmanagedType.LPWStr)] string gamePath, [MarshalAs(UnmanagedType.LPWStr)] string loadInfoJson);

        public bool HoldForUpdate(DirectoryInfo gamePath)
        {
            Log.Information("[HOOKS] DalamudLauncher::HoldForUpdate(gp:{0})", gamePath.FullName);

            var runnerErrorMessage = Loc.Localize("DalamudRunnerError",
                "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".");

            if (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
                DalamudUpdater.ShowOverlay();

            while (DalamudUpdater.State != DalamudUpdater.DownloadState.Done)
            {
                if (DalamudUpdater.State == DalamudUpdater.DownloadState.Failed)
                {
                    DalamudUpdater.CloseOverlay();
                    return false;
                }

                if (DalamudUpdater.State == DalamudUpdater.DownloadState.NoIntegrity)
                {
                    DalamudUpdater.CloseOverlay();

                    CustomMessageBox.Show(runnerErrorMessage,
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                Thread.Yield();
            }

            if (!DalamudUpdater.Runner.Exists)
            {
                CustomMessageBox.Show(runnerErrorMessage,
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!ReCheckVersion(gamePath))
            {
                DalamudUpdater.SetOverlayProgress(DalamudLoadingOverlay.DalamudLoadingProgress.Unavailable);
                DalamudUpdater.ShowOverlay();
                Log.Error("[HOOKS] ReCheckVersion fail.");

                return false;
            }

            return true;
        }

        private void Run(DirectoryInfo gamePath, ClientLanguage language, Process gameProcess)
        {
            Log.Information("[HOOKS] DalamudLauncher::Run(gp:{0}, cl:{1}, pid:{2})", gamePath.FullName, language, gameProcess.Id);

            if (!CheckVcRedist())
                return;

            var ingamePluginPath = Path.Combine(Paths.RoamingPath, "installedPlugins");
            var defaultPluginPath = Path.Combine(Paths.RoamingPath, "devPlugins");

            Directory.CreateDirectory(ingamePluginPath);
            Directory.CreateDirectory(defaultPluginPath);

            var runnerErrorMessage = Loc.Localize("DalamudRunnerError",
                "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".");

            var startInfo = new DalamudStartInfo
            {
                Language = language,
                PluginDirectory = ingamePluginPath,
                DefaultPluginDirectory = defaultPluginPath,
                ConfigurationPath = DalamudSettings.ConfigPath,
                AssetDirectory = DalamudUpdater.AssetDirectory.FullName,
                GameVersion = Repository.Ffxiv.GetVer(gamePath),
                OptOutMbCollection = _optOutMbCollection,
                WorkingDirectory = DalamudUpdater.Runner.Directory?.FullName,
                DelayInitializeMs = (int) App.Settings.DalamudInjectionDelayMs,
            };

            Log.Information("[HOOKS] DelayInitializeMs: {0}", startInfo.DelayInitializeMs);

            if (_loadMethod == DalamudLoadMethod.EntryPoint)
            {
                SetDllDirectory(DalamudUpdater.Runner.DirectoryName);
                try
                {
                    if (0 != RewriteRemoteEntryPointW(gameProcess.Handle,
                            Path.Combine(_gamePath.FullName, "game", gameProcess.ProcessName + ".exe"),
                            JsonConvert.SerializeObject(startInfo)))
                    {
                        CustomMessageBox.Show(runnerErrorMessage,
                            "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                catch (DllNotFoundException)
                {
                    CustomMessageBox.Show(Loc.Localize("AntivirusDeletedBoot", "The Dalamud boot DLL could not be found.\n\nIt was likely deleted by your antivirus software. Please add an exception for the XIVLauncher folder and try again."),
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else if (_loadMethod == DalamudLoadMethod.DllInject)
            {
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
            }
            else
            {
                // should not reach
                throw new ArgumentOutOfRangeException();
            }

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

            if (DalamudUpdater.RunnerOverride != null)
                return true;

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
            var checkForVc2019 = CheckVc2019(); // this also checks all the dll locations now

            if (checkForVc2019)
                return true;

            Log.Error("VC 2015-2019 redistributable not found");

            CustomMessageBox.Show(
                Loc.Localize("DalamudVc2019RedistError",
                    "The XIVLauncher in-game addon needs the Microsoft Visual C++ 2015-2019 redistributable to be installed to continue. Please install it from the Microsoft homepage."),
                "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Exclamation);

            return false;
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