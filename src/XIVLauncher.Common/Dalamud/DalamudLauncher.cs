using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.PlatformAbstractions;

namespace XIVLauncher.Common.Dalamud
{
    public class DalamudLauncher
    {
        private readonly DalamudLoadMethod loadMethod;
        private readonly DirectoryInfo gamePath;
        private readonly ClientLanguage language;
        private readonly IDalamudRunner runner;
        private readonly IDalamudCompatibilityCheck compatCheck;
        private readonly DalamudUpdater updater;
        private readonly int injectionDelay;

        public DalamudLauncher(IDalamudRunner runner, IDalamudCompatibilityCheck compatCheck, DalamudUpdater updater, DalamudLoadMethod loadMethod, ISettings settings)
        {
            this.runner = runner;
            this.compatCheck = compatCheck;
            this.updater = updater;
            this.loadMethod = loadMethod;
            this.gamePath = settings.GamePath;
            this.language = settings.ClientLanguage.GetValueOrDefault(ClientLanguage.English);
            this.injectionDelay = settings.DalamudInjectionDelayMs;
        }

        public const string REMOTE_BASE = "https://goatcorp.github.io/dalamud-distrib/";

        public bool HoldForUpdate(DirectoryInfo gamePath)
        {
            Log.Information("[HOOKS] DalamudLauncher::HoldForUpdate(gp:{0})", gamePath.FullName);

            //var runnerErrorMessage = Loc.Localize("DalamudRunnerError",
            //    "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".");

            if (this.updater.State != DalamudUpdater.DownloadState.Done)
                this.updater.ShowOverlay();

            while (this.updater.State != DalamudUpdater.DownloadState.Done)
            {
                if (this.updater.State == DalamudUpdater.DownloadState.Failed)
                {
                    this.updater.CloseOverlay();
                    return false;
                }

                if (this.updater.State == DalamudUpdater.DownloadState.NoIntegrity)
                {
                    this.updater.CloseOverlay();
                    throw new DalamudRunnerException("No runner integrity");
                }

                Thread.Yield();
            }

            if (!this.updater.Runner.Exists)
                throw new DalamudRunnerException("Runner not present");

            if (!ReCheckVersion(gamePath))
            {
                this.updater.SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Unavailable);
                this.updater.ShowOverlay();
                Log.Error("[HOOKS] ReCheckVersion fail");

                return false;
            }

            return true;
        }

        public void Run(Process gameProcess) // TODO(goat): hook up
        {
            Log.Information("[HOOKS] DalamudLauncher::Run(gp:{0}, cl:{1}, pid:{2})", this.gamePath.FullName, this.language, gameProcess.Id);

            this.compatCheck.EnsureCompatibility();

            var ingamePluginPath = Path.Combine(Paths.RoamingPath, "installedPlugins");
            var defaultPluginPath = Path.Combine(Paths.RoamingPath, "devPlugins");

            Directory.CreateDirectory(ingamePluginPath);
            Directory.CreateDirectory(defaultPluginPath);

            //var runnerErrorMessage = Loc.Localize("DalamudRunnerError",
            //    "Could not launch Dalamud successfully. This might be caused by your antivirus.\nTo prevent this, please add an exception for the folder \"%AppData%\\XIVLauncher\\addons\".");

            var startInfo = new DalamudStartInfo
            {
                Language = language,
                PluginDirectory = ingamePluginPath,
                DefaultPluginDirectory = defaultPluginPath,
                ConfigurationPath = DalamudSettings.ConfigPath,
                AssetDirectory = this.updater.AssetDirectory.FullName,
                GameVersion = Repository.Ffxiv.GetVer(gamePath),
                WorkingDirectory = this.updater.Runner.Directory?.FullName,
                DelayInitializeMs = this.injectionDelay,
            };

            Log.Information("[HOOKS] DelayInitializeMs: {0}", startInfo.DelayInitializeMs);

            this.runner.Run(gameProcess, this.updater.Runner, startInfo, this.gamePath, this.loadMethod);

            this.updater.CloseOverlay();

            Log.Information("[HOOKS] Started dalamud!");
        }

        private bool ReCheckVersion(DirectoryInfo gamePath)
        {
            if (this.updater.State != DalamudUpdater.DownloadState.Done)
                return false;

            if (this.updater.RunnerOverride != null)
                return true;

            var info = DalamudVersionInfo.Load(new FileInfo(Path.Combine(this.updater.Runner.DirectoryName!,
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

        /*
         TODO(goat): hook up
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
        */
    }
}