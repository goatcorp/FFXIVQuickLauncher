using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CheapLoc;
using Config.Net;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Windows;
using XIVLauncher.PlatformAbstractions;
using XIVLauncher.Settings;
using XIVLauncher.Settings.Parsers;
using XIVLauncher.Support;
using XIVLauncher.Windows;

namespace XIVLauncher
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public const string REPO_URL = "https://github.com/goatcorp/FFXIVQuickLauncher";

        public static ILauncherSettingsV3 Settings;
        public static ISteam Steam;
        public static CommonUniqueIdCache UniqueIdCache;

#if !XL_NOAUTOUPDATE
        private UpdateLoadingDialog _updateWindow;
#endif

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

            // TODO: Use a real command line parser
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--roamingPath=", StringComparison.Ordinal))
                {
                    Paths.OverrideRoamingPath(arg.Substring(14));
                }
                else if (arg.StartsWith("--dalamudRunner=", StringComparison.Ordinal))
                {
                    DalamudUpdater.RunnerOverride = new FileInfo(arg.Substring(16));
                }
            }

            var release = $"xivlauncher-{AppUtil.GetAssemblyVersion()}-{AppUtil.GetGitHash()}";

            try
            {
                Log.Logger = new LoggerConfiguration()
                             .WriteTo.Async(a =>
                                 a.File(Path.Combine(Paths.RoamingPath, "output.log")))
                             .WriteTo.Sink(SerilogEventSink.Instance)
#if DEBUG
                             .WriteTo.Debug()
                             .MinimumLevel.Verbose()
#else
                            .MinimumLevel.Information()
#endif
                             .CreateLogger();

#if !DEBUG
                AppDomain.CurrentDomain.UnhandledException += EarlyInitExceptionHandler;
                TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
#endif
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not set up logging. Please report this error.\n\n" + ex.Message, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            SerilogEventSink.Instance.LogLine += OnSerilogLogLine;

            try
            {
                SetupSettings();
            }
            catch (Exception e)
            {
                Log.Error(e, "Settings were corrupted, resetting");
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

            Log.Information(
                $"XIVLauncher started as {release}");

            Steam = new WindowsSteam();

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
                    MessageBox.Show(
                        "XIVLauncher could not contact the update server. Please check your internet connection or try again.\n\n" + ex,
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(0);
                }
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
                       .Build();

            if (string.IsNullOrEmpty(Settings.AcceptLanguage))
            {
                Settings.AcceptLanguage = ApiHelpers.GenerateAcceptLanguage();
            }

            UniqueIdCache = new CommonUniqueIdCache(new FileInfo(Path.Combine(Paths.RoamingPath, "uidCache.json")));
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
            var accountName = string.Empty;

            if (e.Args.Length > 0)
            {
                foreach (string arg in e.Args)
                {
                    if (arg == "--noautologin")
                    {
                        GlobalIsDisableAutologin = true;
                    }

                    if (arg == "--genLocalizable")
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
                        return;
                    }

                    if (arg == "--genIntegrity")
                    {
                        var result = IntegrityCheck.RunIntegrityCheckAsync(Settings.GamePath, null).GetAwaiter().GetResult();
                        string saveIntegrityPath = Path.Combine(Paths.RoamingPath, $"{result.GameVersion}.json");
#if DEBUG
                        Log.Information("Saving integrity to " + saveIntegrityPath);
#endif
                        File.WriteAllText(saveIntegrityPath, JsonConvert.SerializeObject(result));

                        MessageBox.Show($"Successfully hashed {result.Hashes.Count} files.");
                        Environment.Exit(0);
                        return;
                    }

                    // Check if the accountName parameter is provided, if yes, pass it to MainWindow
                    if (arg.StartsWith("--account=", StringComparison.Ordinal))
                    {
                        accountName = arg.Substring(arg.IndexOf("=", StringComparison.InvariantCulture) + 1);
                        App.Settings.CurrentAccountId = accountName;
                    }

                    // Check if the steam ticket parameter is provided, use it later to skip steam integration
                    if (arg.StartsWith("--steamticket=", StringComparison.Ordinal))
                    {
                        string steamTicket = arg.Substring(arg.IndexOf("=", StringComparison.InvariantCulture) + 1);
                        GlobalSteamTicket = Convert.FromBase64String(steamTicket);
                    }

                    // Override client launch language by parameter
                    if (arg.StartsWith("--clientlang=", StringComparison.Ordinal))
                    {
                        string langarg = arg.Substring(arg.IndexOf("=", StringComparison.InvariantCulture) + 1);
                        Enum.TryParse(langarg, out ClientLanguage lang); // defaults to Japanese if the input was invalid.
                        App.Settings.Language = lang;
                        Log.Information("Language set as {Language} by launch argument", App.Settings.Language.ToString());
                    }
                }
            }

            Log.Verbose("Loading MainWindow for account '{0}'", accountName);

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

            if (EnvironmentSettings.IsDisableUpdates)
            {
                OnUpdateCheckFinished(true);
            }

#if XL_NOAUTOUPDATE
            OnUpdateCheckFinished(true);
#endif
        }
    }
}