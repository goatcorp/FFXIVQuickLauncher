using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CheapLoc;
using CommandLine;
using Config.Net;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Support;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Windows;
using XIVLauncher.PlatformAbstractions;
using XIVLauncher.Settings;
using XIVLauncher.Settings.Parsers;
using XIVLauncher.Support;
using XIVLauncher.Windows;
using XIVLauncher.Xaml;

namespace XIVLauncher
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public class CmdLineOptions
        {
            [CommandLine.Option("dalamud-runner-override", Required = false, HelpText = "Path to a folder to override the dalamud runner with.")]
            public string RunnerOverride { get; set; }

            [CommandLine.Option("roamingPath", Required = false, HelpText = "Path to a folder to override the roaming path for XL with.")]
            public string RoamingPath { get; set; }

            [CommandLine.Option("noautologin", Required = false, HelpText = "Disable autologin.")]
            public bool NoAutoLogin { get; set; }

            [CommandLine.Option("gen-localizable", Required = false, HelpText = "Generate localizable files.")]
            public bool DoGenerateLocalizables { get; set; }

            [CommandLine.Option("gen-integrity", Required = false, HelpText = "Generate integrity files. Provide a game path.")]
            public string DoGenerateIntegrity { get; set; }

            [CommandLine.Option("account", Required = false, HelpText = "Account name to use.")]
            public string AccountName { get; set; }

            [CommandLine.Option("steamticket", Required = false, HelpText = "Steam ticket to use.")]
            public string SteamTicket { get; set; }

            [CommandLine.Option("clientlang", Required = false, HelpText = "Client language to use.")]
            public ClientLanguage? ClientLanguage { get; set; }

            // We don't care about these, just need it so that the parser doesn't error
            [CommandLine.Option("squirrel-updated", Hidden = true)]
            public string SquirrelUpdated { get; set; }

            [CommandLine.Option("squirrel-install", Hidden = true)]
            public string SquirrelInstall { get; set; }

            [CommandLine.Option("squirrel-obsolete", Hidden = true)]
            public string SquirrelObsolete { get; set; }

            [CommandLine.Option("squirrel-uninstall", Hidden = true)]
            public string SquirrelUninstall { get; set; }

            [CommandLine.Option("squirrel-firstrun", Hidden = true)]
            public bool SquirrelFirstRun { get; set; }
        }

        public const string REPO_URL = "https://github.com/goatcorp/FFXIVQuickLauncher";

        public static ILauncherSettingsV3 Settings;
        public static WindowsSteam Steam;
        public static CommonUniqueIdCache UniqueIdCache;

#if !XL_NOAUTOUPDATE
        private UpdateLoadingDialog _updateWindow;
#endif

        public static CmdLineOptions CommandLine { get; private set; }

        private FileInfo _dalamudRunnerOverride = null;
        private MainWindow _mainWindow;

        public static bool GlobalIsDisableAutologin { get; private set; }
        public static byte[] GlobalSteamTicket { get; private set; }
        public static DalamudUpdater DalamudUpdater { get; private set; }

        public static Brush UaBrush = new LinearGradientBrush(new GradientStopCollection()
        {
            new(Color.FromArgb(0xFF, 0x00, 0x57, 0xB7), 0.5f),
            new(Color.FromArgb(0xFF, 0xFF, 0xd7, 0x00), 0.5f),
        }, 0.7f);

        public App()
        {
#if !DEBUG
            try
            {
                AppDomain.CurrentDomain.UnhandledException += EarlyInitExceptionHandler;
                TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            }
            catch
            {
                // ignored
            }
#endif
        }

        private static void OnSerilogLogLine(object sender, (string Line, LogEventLevel Level, DateTimeOffset TimeStamp, Exception? Exception) e)
        {
            if (e.Exception == null)
                return;

            Troubleshooting.LogException(e.Exception, e.Line);
        }

        private void SetupSettings()
        {
            Settings = new ConfigurationBuilder<ILauncherSettingsV3>()
                       .UseCommandLineArgs()
                       .UseJsonFile(GetConfigPath("launcher"))
                       .UseTypeParser(new DirectoryInfoParser())
                       .UseTypeParser(new AddonListParser())
                       .UseTypeParser(new CommonJsonParser<PreserveWindowPosition.WindowPlacement>())
                       .Build();

            if (string.IsNullOrEmpty(Settings.AcceptLanguage))
            {
                Settings.AcceptLanguage = ApiHelpers.GenerateAcceptLanguage();
            }

            UniqueIdCache = new CommonUniqueIdCache(new FileInfo(Path.Combine(Paths.RoamingPath, "uidCache.json")));

            try
            {
                if (!string.IsNullOrEmpty(CommandLine.AccountName))
                {
                    App.Settings.CurrentAccountId = CommandLine.AccountName;
                    Log.Verbose("Account override: '{0}'", CommandLine.AccountName);
                }

                if (!string.IsNullOrEmpty(CommandLine.SteamTicket))
                {
                    GlobalSteamTicket = Convert.FromBase64String(CommandLine.SteamTicket);
                }

                if (CommandLine.ClientLanguage != null)
                {
                    App.Settings.Language = CommandLine.ClientLanguage;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not apply settings overrides from command line");
            }
        }

        private void OnUpdateCheckFinished(bool finishUp)
        {
            Dispatcher.Invoke(() =>
            {
                _useFullExceptionHandler = true;

#if !XL_NOAUTOUPDATE
                if (_updateWindow != null)
                    _updateWindow.Hide();
#endif

                if (!finishUp)
                    return;

                _mainWindow = new MainWindow();
                _mainWindow.Initialize();

                try
                {
                    DalamudUpdater = new DalamudUpdater(new DirectoryInfo(Path.Combine(Paths.RoamingPath, "addon")),
                        new DirectoryInfo(Path.Combine(Paths.RoamingPath, "runtime")),
                        new DirectoryInfo(Path.Combine(Paths.RoamingPath, "dalamudAssets")),
                        new DirectoryInfo(Paths.RoamingPath),
                        UniqueIdCache,
                        Settings.DalamudRolloutBucket);

                    if (this._dalamudRunnerOverride != null)
                    {
                        DalamudUpdater.RunnerOverride = this._dalamudRunnerOverride;
                    }

                    Settings.DalamudRolloutBucket = DalamudUpdater.RolloutBucket;

                    var dalamudWindowThread = new Thread(DalamudOverlayThreadStart);
                    dalamudWindowThread.SetApartmentState(ApartmentState.STA);
                    dalamudWindowThread.IsBackground = true;
                    dalamudWindowThread.Start();

                    while (DalamudUpdater.Overlay == null)
                        Thread.Yield();

                    DalamudUpdater.Run();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not start dalamud updater");
                }
            });
        }

        // We need this because the main dispatcher is blocked by the main window/login task.
        private static void DalamudOverlayThreadStart()
        {
            var overlay = new DalamudLoadingOverlay();
            overlay.Hide();

            DalamudUpdater.Overlay = overlay;

            System.Windows.Threading.Dispatcher.Run();
        }

        private static void GenerateIntegrity(string path)
        {
            var result = IntegrityCheck.RunIntegrityCheckAsync(new DirectoryInfo(path), null).GetAwaiter().GetResult();
            string saveIntegrityPath = Path.Combine(Paths.RoamingPath, $"{result.GameVersion}.json");

            File.WriteAllText(saveIntegrityPath, JsonConvert.SerializeObject(result));

            MessageBox.Show($"Successfully hashed {result.Hashes.Count} files to {path}.", "Hello Franz", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            Environment.Exit(0);
        }

        private static void GenerateLocalizables()
        {
            try
            {
                Loc.ExportLocalizable();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            Environment.Exit(0);
        }

        private bool _useFullExceptionHandler = false;

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (!e.Observed)
                EarlyInitExceptionHandler(sender, new UnhandledExceptionEventArgs(e.Exception, true));
        }

        private void EarlyInitExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                Log.Error((Exception)e.ExceptionObject, "Unhandled exception");

                if (_useFullExceptionHandler)
                {
                    CustomMessageBox.Builder
                                    .NewFrom((Exception)e.ExceptionObject, "Unhandled", CustomMessageBox.ExitOnCloseModes.ExitOnClose)
                                    .WithAppendText("\n\nError during early initialization. Please report this error.\n\n" + e.ExceptionObject)
                                    .Show();
                }
                else
                {
                    MessageBox.Show(
                        "Error during early initialization. Please report this error.\n\n" + e.ExceptionObject,
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                Environment.Exit(-1);
            });
        }

        private static string GetConfigPath(string prefix) => Path.Combine(Paths.RoamingPath, $"{prefix}ConfigV3.json");

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            // HW rendering commonly causes issues with material design, so we turn it off by default for now
            try
            {
                if (!EnvironmentSettings.IsHardwareRendered)
                    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
            catch
            {
                // ignored
            }

            try
            {
                LogInit.Setup(
                    Path.Combine(Paths.RoamingPath, "output.log"),
                    Environment.GetCommandLineArgs());

                Log.Information("========================================================");
                Log.Information("Starting a session(v{Version} - {Hash})", AppUtil.GetAssemblyVersion(), AppUtil.GetGitHash());

                SerilogEventSink.Instance.LogLine += OnSerilogLogLine;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not set up logging. Please report this error.\n\n" + ex.Message, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            try
            {
                var helpWriter = new StringWriter();
                var parser = new Parser(config =>
                {
                    config.HelpWriter = helpWriter;
                    config.IgnoreUnknownArguments = true;
                });
                var result = parser.ParseArguments<CmdLineOptions>(Environment.GetCommandLineArgs());

                if (result.Errors.Any())
                {
                    MessageBox.Show(helpWriter.ToString(), "Help");
                }

                CommandLine = result.Value ?? new CmdLineOptions();

                if (!string.IsNullOrEmpty(CommandLine.RoamingPath))
                {
                    Paths.OverrideRoamingPath(CommandLine.RoamingPath);
                }

                if (!string.IsNullOrEmpty(CommandLine.RunnerOverride))
                {
                    this._dalamudRunnerOverride = new FileInfo(CommandLine.RunnerOverride);
                }

                if (CommandLine.NoAutoLogin)
                {
                    GlobalIsDisableAutologin = true;
                }

                if (!string.IsNullOrEmpty(CommandLine.DoGenerateIntegrity))
                {
                    GenerateIntegrity(CommandLine.DoGenerateIntegrity);
                }

                if (CommandLine.DoGenerateLocalizables)
                {
                    GenerateLocalizables();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not parse command line arguments. Please report this error.\n\n" + ex.Message, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            try
            {
                SetupSettings();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Settings were corrupted, resetting");
                File.Delete(GetConfigPath("launcher"));
                SetupSettings();
            }

#if !XL_LOC_FORCEFALLBACKS
            try
            {
                if (App.Settings.LauncherLanguage == null)
                {
                    var currentUiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                    App.Settings.LauncherLanguage = App.Settings.LauncherLanguage.GetLangFromTwoLetterIso(currentUiLang);
                }

                Log.Information("Trying to set up Loc for language code {0}", App.Settings.LauncherLanguage.GetLocalizationCode());

                if (!App.Settings.LauncherLanguage.IsDefault())
                {
                    Loc.Setup(AppUtil.GetFromResources($"XIVLauncher.Resources.Loc.xl.xl_{App.Settings.LauncherLanguage.GetLocalizationCode()}.json"));
                }
                else
                {
                    Loc.SetupWithFallbacks();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not get language information. Setting up fallbacks.");
                Loc.Setup("{}");
            }
#else
            // Force all fallbacks
            Loc.Setup("{}");
#endif

            try
            {
                Steam = new WindowsSteam();

                if (Settings.AutoStartSteam.GetValueOrDefault(false))
                    Steam.KickoffAsyncStartup(Settings.IsFt.GetValueOrDefault(false) ? Constants.STEAM_FT_APP_ID : Constants.STEAM_APP_ID);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not set up Steam");
            }

#if !XL_NOAUTOUPDATE
            if (!EnvironmentSettings.IsDisableUpdates)
            {
                try
                {
                    Log.Information("Starting update check...");

                    _updateWindow = new UpdateLoadingDialog();
                    _updateWindow.Show();

                    var updateMgr = new Updates();
                    updateMgr.OnUpdateCheckFinished += OnUpdateCheckFinished;

                    ChangelogWindow changelogWindow = null;

                    try
                    {
                        changelogWindow = new ChangelogWindow(EnvironmentSettings.IsPreRelease);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not load changelog window");
                    }

                    Task.Run(() => updateMgr.Run(EnvironmentSettings.IsPreRelease, changelogWindow));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not dispatch update check");
                    MessageBox.Show(
                        "XIVLauncher could not check for updates. Please check your internet connection or try again.\n\n" + ex,
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                    return;
                }
            }
#endif

            try
            {
                if (App.Settings.LauncherLanguage == LauncherLanguage.Russian)
                {
                    var dict = new ResourceDictionary
                    {
                        { "PrimaryHueLightBrush", UaBrush },
                        //{"PrimaryHueLightForegroundBrush", uaBrush},
                        { "PrimaryHueMidBrush", UaBrush },
                        //{"PrimaryHueMidForegroundBrush", uaBrush},
                        { "PrimaryHueDarkBrush", UaBrush },
                        //{"PrimaryHueDarkForegroundBrush", uaBrush},
                    };
                    this.Resources.MergedDictionaries.Add(dict);
                }
            }
            catch
            {
                // ignored
            }

            if (EnvironmentSettings.IsDisableUpdates)
            {
                OnUpdateCheckFinished(true);
                return;
            }

#if XL_NOAUTOUPDATE
            OnUpdateCheckFinished(true);
#endif
        }
    }
}
