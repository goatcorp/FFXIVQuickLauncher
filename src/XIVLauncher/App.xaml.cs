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
using XIVLauncher.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Parsers;
using XIVLauncher.Support;
using XIVLauncher.Windows;

namespace XIVLauncher
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public const string RepoUrl = "https://github.com/goatcorp/FFXIVQuickLauncher";

        public static ILauncherSettingsV3 Settings;

#if !XL_NOAUTOUPDATE
        private UpdateLoadingDialog _updateWindow;
#endif

        private MainWindow _mainWindow;

        public static bool GlobalIsDisableAutologin { get; private set; }

        public App()
        {
            // wine does not support WPF with HW rendering, so switch to software only mode
            try
            {
                if (EnvironmentSettings.IsWine)
                    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            }
            catch
            {
                // ignored
            }

            // TODO: Use a real command line parser
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("--roamingPath="))
                {
                    Paths.OverrideRoamingPath(arg.Substring(14));
                }
                else if (arg.StartsWith("--dalamudRunner="))
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
                    App.Settings.LauncherLanguage = App.Settings.LauncherLanguage.GetLangFromTwoLetterISO(currentUiLang);
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
            catch(Exception ex){
                Log.Error(ex, "Could not get language information. Setting up fallbacks.");
                Loc.Setup("{}");
            }
#else
            // Force all fallbacks
            Loc.Setup("{}");
#endif

            Log.Information(
                $"XIVLauncher started as {release}");

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

                    var _ = updateMgr.Run(EnvironmentSettings.IsPreRelease, changelogWindow);
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
                Settings.AcceptLanguage = Util.GenerateAcceptLanguage();
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

                var newWindowThread = new Thread(DalamudOverlayThreadStart);
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.IsBackground = true;
                newWindowThread.Start();

                while (DalamudUpdater.Overlay == null)
                    Thread.Yield();

                DalamudUpdater.Run();
            });
        }

        // We need this because the main dispatcher is blocked by the main window/login task.
        private static void DalamudOverlayThreadStart()
        {
            DalamudUpdater.Overlay = new DalamudLoadingOverlay();
            DalamudUpdater.Overlay.Hide();

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
                Log.Error((Exception) e.ExceptionObject, "Unhandled exception.");

                if (_useFullExceptionHandler)
                    ErrorWindow.Show((Exception) e.ExceptionObject, "An unhandled exception occurred.", "Unhandled");
                else
                    MessageBox.Show(
                        "Error during early initialization. Please report this error.\n\n" + e.ExceptionObject,
                        "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

                Log.CloseAndFlush();
                Environment.Exit(0);
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
                    if (arg.StartsWith("--account="))
                    {
                        accountName = arg.Substring(arg.IndexOf("=", StringComparison.InvariantCulture) + 1);
                        App.Settings.CurrentAccountId = accountName;
                    }

                    // Override client launch language by parameter
                    if (arg.StartsWith("--clientlang="))
                    {
                        string langarg = arg.Substring(arg.IndexOf("=", StringComparison.InvariantCulture) + 1);
                        Enum.TryParse(langarg, out ClientLanguage lang); // defaults to Japanese if the input was invalid.
                        App.Settings.Language = lang;
                        Log.Information($"Language set as {App.Settings.Language.ToString()} by launch argument.");
                    }
                }
            }

            Log.Verbose("Loading MainWindow for account '{0}'", accountName);

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