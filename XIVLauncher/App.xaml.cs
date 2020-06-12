using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CheapLoc;
using Config.Net;
using Newtonsoft.Json;
using Sentry;
using Serilog;
using Serilog.Events;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;
using XIVLauncher.Settings;
using XIVLauncher.Settings.Parsers;
using XIVLauncher.Windows;

namespace XIVLauncher
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static ILauncherSettingsV3 Settings;

        private UpdateLoadingDialog _updateWindow;

        private readonly string[] _allowedLang = {"de", "ja", "fr", "it", "es"};

        private MainWindow _mainWindow;

        public App()
        {
            Settings = new ConfigurationBuilder<ILauncherSettingsV3>()
                .UseCommandLineArgs()
                .UseJsonFile(GetConfigPath("launcher"))
                .UseTypeParser(new DirectoryInfoParser())
                .UseTypeParser(new AddonListParser())
                .Build();

            var release = $"xivlauncher-{Util.GetAssemblyVersion()}-{Util.GetGitHash()}";

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a =>
                    a.File(Path.Combine(Paths.XIVLauncherPath, "output.log")))
#if DEBUG
                .WriteTo.Debug()
                .MinimumLevel.Verbose()
#else
                .MinimumLevel.Information()
                .WriteTo.Sentry(o =>
                {
                    o.MinimumBreadcrumbLevel = LogEventLevel.Debug; // Debug and higher are stored as breadcrumbs (default is Information)
                    o.MinimumEventLevel = LogEventLevel.Error; // Error and higher is sent as event (default is Error)
                    // If DSN is not set, the SDK will look for an environment variable called SENTRY_DSN. If nothing is found, SDK is disabled.
                    o.Dsn = new Dsn("https://53970fece4974473b84157b45a47e54f@sentry.io/1548116");
                    o.AttachStacktrace = true;
                    o.SendDefaultPii = false; // send PII like the username of the user logged in to the device

                    o.Release = release;
                })
#endif
                .CreateLogger();


#if !XL_LOC_FORCEFALLBACKS
            try
            {
                var currentUiLang = CultureInfo.CurrentUICulture;
                Log.Information("Trying to set up Loc for culture {0}", currentUiLang.TwoLetterISOLanguageName);

                Loc.Setup(_allowedLang.Any(x => currentUiLang.TwoLetterISOLanguageName == x)
                    ? $"Loc.xl.xl_{currentUiLang.TwoLetterISOLanguageName}.json"
                    : "{}");
            }
            catch(Exception ex){
                Log.Error(ex, "Could not get language information. Setting up fallbacks.");
                Loc.Setup("{}");
            }  
#else
            // Force all fallbacks
            Loc.Setup("{}");
#endif

#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += EarlyInitExceptionHandler;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
#endif

            Log.Information(
                $"XIVLauncher started as {release}");

#if !XL_NOAUTOUPDATE
            try
            {
                Log.Information("Starting update check...");

                _updateWindow = new UpdateLoadingDialog();
                _updateWindow.Show();

                var updateMgr = new Updates();
                updateMgr.OnUpdateCheckFinished += OnUpdateCheckFinished;

                updateMgr.Run(Environment.GetEnvironmentVariable("XL_PRERELEASE") == "True");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "XIVLauncher could not contact the update server. Please check your internet connection or try again.\n\n" + ex,
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
#endif
        }

        private void OnUpdateCheckFinished(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _useFullExceptionHandler = true;

                if (_updateWindow != null) 
                    _updateWindow.Hide();

                _mainWindow.Initialize();
            });
        }

        private bool _useFullExceptionHandler = false;

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (!e.Observed)
                EarlyInitExceptionHandler(sender, new UnhandledExceptionEventArgs(e.Exception, true));
        }

        private void EarlyInitExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            
            if (_useFullExceptionHandler)
                new ErrorWindow((Exception)e.ExceptionObject, "An unhandled exception occured.", "Unhandled")
                    .ShowDialog();
            else
                MessageBox.Show(
                    "Error during early initialization. Please report this error.\n\n" + e.ExceptionObject,
                    "XIVLauncher Error", MessageBoxButton.OK, MessageBoxImage.Error);

            Log.CloseAndFlush();
            Environment.Exit(0);
        }

        private static string GetConfigPath(string prefix) => Path.Combine(Paths.XIVLauncherPath, $"{prefix}ConfigV3.json");

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0 && e.Args[0] == "--genLocalizable")
            {
                Loc.ExportLocalizable();
                Environment.Exit(0);
                return;
            }

            if (e.Args.Length > 0 && e.Args[0] == "--genIntegrity")
            {
                var result = IntegrityCheck.RunIntegrityCheckAsync(Settings.GamePath, null).GetAwaiter().GetResult();
                File.WriteAllText($"{result.GameVersion}.json", JsonConvert.SerializeObject(result));

                MessageBox.Show($"Successfully hashed {result.Hashes.Count} files.");
                Environment.Exit(0);
                return;
            }

            // Check if the accountName parameter is provided, if yes, pass it to MainWindow
            var accountName = string.Empty;

            if (e.Args.Length > 0 && e.Args[0].StartsWith("--account="))
                accountName = e.Args[0].Substring(e.Args[0].IndexOf("=", StringComparison.InvariantCulture) + 1);

            Log.Information("Loading MainWindow for account '{0}'", accountName);
            _mainWindow = new MainWindow(accountName);

#if XL_NOAUTOUPDATE
            OnUpdateCheckFinished(null, null);
#endif
        }
    }
}