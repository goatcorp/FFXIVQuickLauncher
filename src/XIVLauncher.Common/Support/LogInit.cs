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
        Parser.Default.ParseArguments<LogOptions>(args)
              .WithParsed(o =>
              {
                  var config = new LoggerConfiguration()
                               .WriteTo.Sink(SerilogEventSink.Instance);

                  if (!string.IsNullOrEmpty(o.LogPath))
                  {
                      config.WriteTo.Async(a =>
                      {
                          a.File(o.LogPath);
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

                  if (o.Verbose)
                      config.MinimumLevel.Verbose();

                  Log.Logger = config.CreateLogger();
              });
    }
}
