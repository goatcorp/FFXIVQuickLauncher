using System;
using System.IO;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;
using Sentry;
using Serilog;
using Serilog.Events;
using XIVLauncher.Addon;
using XIVLauncher.Addon.Implementations;
using XIVLauncher.Dalamud;
using XIVLauncher.Game;
using XIVLauncher.Windows;

namespace XIVLauncher
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            var culture = new System.Globalization.CultureInfo("de-DE");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            var release = $"xivlauncher-{Util.GetAssemblyVersion()}-{Util.GetGitHash()}";

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a =>
                    a.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "XIVLauncher", "output.log")))
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

            Log.Information(
                $"XIVLauncher started as {release}");
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            // Check if dark mode is enabled on windows, if yes, load the dark theme
            var themeUri =
                new Uri(
                    "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml",
                    UriKind.RelativeOrAbsolute);
            if (Util.IsWindowsDarkModeEnabled())
                themeUri = new Uri(
                    "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml",
                    UriKind.RelativeOrAbsolute);

            Current.Resources.MergedDictionaries.Add(new ResourceDictionary {Source = themeUri});
            Log.Information("Loaded UI theme resource.");

#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                new ErrorWindow((Exception) args.ExceptionObject, "An unhandled exception occured.", "Unhandled")
                    .ShowDialog();
                Log.CloseAndFlush();
                Environment.Exit(0);
            };
#endif

            if (e.Args.Length > 0 && e.Args[0] == "--backupNow")
            {
                (new CharacterBackupAddon() as INotifyAddonAfterClose).GameClosed();

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

            if (e.Args.Length > 0 && e.Args[0] == "--dalamudStg")
            {
                Console.Beep();
                DalamudLauncher.UseDalamudStaging = true;
            }

            // Check if the accountName parameter is provided, if yes, pass it to MainWindow
            var accountName = "";

            if (e.Args.Length > 0 && e.Args[0].StartsWith("--account="))
                accountName = e.Args[0].Substring(e.Args[0].IndexOf("=", StringComparison.InvariantCulture) + 1);
            
            Log.Information("Loading MainWindow for account '{0}'", accountName);

            var mainWindow = new MainWindow(accountName);
        }
    }
}