using System.IO;
using System.Linq;
using CommandLine;
using Serilog;

namespace XIVLauncher.Common.Support;

public static class LogInit
{
    // ReSharper disable once ClassNeverInstantiated.Local
    private class LogOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }

        [Option("log-file-path", Required = false, HelpText = "Set path for log file.")]
        public string? LogPath { get; set; }
    }

    public static void Setup(string defaultLogPath, string[] args)
    {
        ParserResult<LogOptions> result = null;

        try
        {
            var parser = new Parser(c => { c.IgnoreUnknownArguments = true; });
            result = parser.ParseArguments<LogOptions>(args);
        }
        catch
        {
#if DEBUG
            throw;
#endif
        }

        var config = new LoggerConfiguration()
                     .WriteTo.Sink(SerilogEventSink.Instance);

        var parsed = result?.Value ?? new LogOptions();

        if (!string.IsNullOrEmpty(parsed.LogPath))
        {
            config.WriteTo.Async(a =>
            {
                a.File(parsed.LogPath);
            });
        }
        else
        {
            config.WriteTo.Async(a =>
            {
                a.File(defaultLogPath);
            });
        }

#if DEBUG
        config.WriteTo.Debug();
        config.MinimumLevel.Verbose();
#else
        config.MinimumLevel.Information();
#endif

        if (parsed.Verbose)
            config.MinimumLevel.Verbose();

        Log.Logger = config.CreateLogger();
    }
}
