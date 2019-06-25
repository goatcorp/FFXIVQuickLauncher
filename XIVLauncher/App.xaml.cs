using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Async(a => a.File(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "output.log")))
#if DEBUG
                .MinimumLevel.Verbose()
#else
                .MinimumLevel.Information()
#endif
                .CreateLogger();

            Log.Information($"XIVLauncher started with version {Util.GetAssemblyVersion()}, commit {Util.GetGitHash()}");
        }

        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            // Check if dark mode is enabled on windows, if yes, load the dark theme
            var themeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Light.xaml", UriKind.RelativeOrAbsolute);
            if (Util.IsWindowsDarkModeEnabled())
                themeUri = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Dark.xaml", UriKind.RelativeOrAbsolute);

            Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = themeUri });
            Serilog.Log.Information("Loaded UI theme resource.");

#if !DEBUG
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                new ErrorWindow((Exception) args.ExceptionObject, "An unhandled exception occured.", "Unhandled").ShowDialog();
                Serilog.Log.CloseAndFlush();
                Environment.Exit(0);
            };
#endif

            var accountName = "";

            if (e.Args.Length > 0 && e.Args[0].StartsWith("--accountName="))
                accountName = e.Args[0].Substring(e.Args[0].IndexOf("=", StringComparison.InvariantCulture) + 1);

            Log.Information("Loading MainWindow for account '{0}'", accountName);

            var window = new MainWindow(accountName);
        }
    }
}
