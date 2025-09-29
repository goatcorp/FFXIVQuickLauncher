using System;
using System.IO;
using System.Windows;
using Serilog;
using Serilog.Events;
using Velopack;
using Velopack.Logging;
using XIVLauncher.Common;
using XIVLauncher.Common.Support;
using XIVLauncher.Support;

namespace XIVLauncher;

public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
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
            MessageBox.Show("Could not set up logging. Please report this error.\n\n" + ex, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        try
        {
            var serilogLogger = new VelopackSerilogLogger();

            VelopackApp.Build()
                       .SetLogger(serilogLogger)
                       .Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not update XIVLauncher. Please report this error.\n\n" + ex, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Now run the WPF app.
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static void OnSerilogLogLine(object sender, (string Line, LogEventLevel Level, DateTimeOffset TimeStamp, Exception Exception) e)
    {
        if (e.Exception == null)
            return;

        Troubleshooting.LogException(e.Exception, e.Line);
    }

    private class VelopackSerilogLogger : IVelopackLogger
    {
        public void Log(VelopackLogLevel logLevel, string message, Exception exception)
        {
            var level = logLevel switch
            {
                VelopackLogLevel.Trace => LogEventLevel.Verbose,
                VelopackLogLevel.Debug => LogEventLevel.Debug,
                VelopackLogLevel.Error => LogEventLevel.Error,
                VelopackLogLevel.Information => LogEventLevel.Information,
                VelopackLogLevel.Warning => LogEventLevel.Warning,
                VelopackLogLevel.Critical => LogEventLevel.Fatal,
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
            };

            Serilog.Log.Write(level, exception, "[VELOPACK] {Message}", message);
        }
    }
}
