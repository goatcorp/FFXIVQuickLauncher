using System.Collections.Generic;
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
        private readonly DirectoryInfo configDirectory;
        private readonly DirectoryInfo logPath;
        private readonly ClientLanguage language;
        private readonly IDalamudRunner runner;
        private readonly DalamudUpdater updater;
        private readonly int injectionDelay;
        private readonly bool fakeLogin;
        private readonly bool noPlugin;
        private readonly bool noThirdPlugin;
        private readonly string troubleshootingData;

        public enum DalamudInstallState
        {
            Ok,
            OutOfDate,
        }

        public DalamudLauncher(IDalamudRunner runner, DalamudUpdater updater, DalamudLoadMethod loadMethod, DirectoryInfo gamePath, DirectoryInfo configDirectory, DirectoryInfo logPath,
                               ClientLanguage clientLanguage, int injectionDelay, bool fakeLogin, bool noPlugin, bool noThirdPlugin, string troubleshootingData)
        {
            this.runner = runner;
            this.updater = updater;
            this.loadMethod = loadMethod;
            this.gamePath = gamePath;
            this.configDirectory = configDirectory;
            this.logPath = logPath;
            this.language = clientLanguage;
            this.injectionDelay = injectionDelay;
            this.fakeLogin = fakeLogin;
            this.noPlugin = noPlugin;
            this.noThirdPlugin = noThirdPlugin;
            this.troubleshootingData = troubleshootingData;
        }

        public const string REMOTE_BASE = "https://kamori.goats.dev/Dalamud/Release/VersionInfo?track=";

        public DalamudInstallState HoldForUpdate(DirectoryInfo gamePath)
        {
            Log.Information("[HOOKS] DalamudLauncher::HoldForUpdate(gp:{0})", gamePath.FullName);

            if (this.updater.State != DalamudUpdater.DownloadState.Done)
                this.updater.ShowOverlay();

            while (this.updater.State != DalamudUpdater.DownloadState.Done)
            {
                if (this.updater.State == DalamudUpdater.DownloadState.NoIntegrity)
                {
                    this.updater.CloseOverlay();
                    throw new DalamudRunnerException("Updater returned no integrity.", this.updater.EnsurementException);
                }

                Thread.Yield();
            }

            if (!this.updater.Runner.Exists)
                throw new DalamudRunnerException("Runner did not exist.");

            if (!ReCheckVersion(gamePath))
            {
                this.updater.SetOverlayProgress(IDalamudLoadingOverlay.DalamudUpdateStep.Unavailable);
                this.updater.ShowOverlay();
                Log.Error("[HOOKS] ReCheckVersion fail");

                return DalamudInstallState.OutOfDate;
            }

            return DalamudInstallState.Ok;
        }

        public Process Run(FileInfo gameExe, string gameArgs, IDictionary<string, string> environment)
        {
            Log.Information("[HOOKS] DalamudLauncher::Run(gp:{0}, cl:{1})", this.gamePath.FullName, this.language);

            var ingamePluginPath = Path.Combine(this.configDirectory.FullName, "installedPlugins");

            Directory.CreateDirectory(ingamePluginPath);

            var startInfo = new DalamudStartInfo
            {
                Language = language,
                PluginDirectory = ingamePluginPath,
                ConfigurationPath = DalamudSettings.GetConfigPath(this.configDirectory),
                LoggingPath = this.logPath.FullName,
                AssetDirectory = this.updater.AssetDirectory.FullName,
                GameVersion = Repository.Ffxiv.GetVer(gamePath),
                WorkingDirectory = this.updater.Runner.Directory?.FullName,
                DelayInitializeMs = this.injectionDelay,
                TroubleshootingPackData = this.troubleshootingData,
            };

            if (this.loadMethod != DalamudLoadMethod.ACLonly)
                Log.Information("[HOOKS] DelayInitializeMs: {0}", startInfo.DelayInitializeMs);

            switch (this.loadMethod)
            {
                case DalamudLoadMethod.EntryPoint:
                    Log.Verbose("[HOOKS] Now running OEP rewrite");
                    break;

                case DalamudLoadMethod.DllInject:
                    Log.Verbose("[HOOKS] Now running DLL inject");
                    break;

                case DalamudLoadMethod.ACLonly:
                    Log.Verbose("[HOOKS] Now running ACL-only fix without injection");
                    break;
            }

            var process = this.runner.Run(this.updater.Runner, this.fakeLogin, this.noPlugin, this.noThirdPlugin, gameExe, gameArgs, environment, this.loadMethod, startInfo);

            this.updater.CloseOverlay();

            if (this.loadMethod != DalamudLoadMethod.ACLonly)
                Log.Information("[HOOKS] Started dalamud!");

            return process;
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

            var versionInfoJson = client.DownloadString(REMOTE_BASE + "release");
            var remoteVersionInfo = JsonConvert.DeserializeObject<DalamudVersionInfo>(versionInfoJson);

            if (Repository.Ffxiv.GetVer(gamePath) != remoteVersionInfo.SupportedGameVer)
                return false;

            return true;
        }
    }
}