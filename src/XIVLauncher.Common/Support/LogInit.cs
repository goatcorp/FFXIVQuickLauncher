using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CommandLine;
using Serilog;
using Serilog.Enrichers.Sensitive;
using XIVLauncher.Common.Util;

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
            if (DebugHelpers.IsDebugBuild)
                throw;
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

        if (DebugHelpers.IsDebugBuild)
        {
            config.WriteTo.Debug();
            config.MinimumLevel.Verbose();
        }
        else
        {
            config.MinimumLevel.Information();
        }

        config.Enrich.WithSensitiveDataMasking(o =>
        {
            o.MaskingOperators = new List<IMaskingOperator>()
            {
                new SeEncryptedArgsMaskingOperator(),
                new SeTestSidMaskingOperator(),
                new UsernameMaskingOperator(),
            };
        });

        if (parsed.Verbose)
            config.MinimumLevel.Verbose();

        Log.Logger = config.CreateLogger();
    }

    private class SeTestSidMaskingOperator() : RegexMaskingOperator(TestSidPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
    {
        private const string TestSidPattern =
            @"(?:DEV\.TestSID=\S+)";

        protected override bool ShouldMaskInput(string input)
        {
            return input != "DEV.TestSID=0";
        }
    }

    private class SeEncryptedArgsMaskingOperator() : RegexMaskingOperator(EncryptedArgsPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
    {
        private const string EncryptedArgsPattern =
            @"(?:\/\/\*\*sqex[0-9]+\S+\/\/)";
    }

    private class UsernameMaskingOperator() : RegexMaskingOperator(UsernamePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)
    {
        private const string UsernamePattern =
            @"(?<=\\Users\\|/home/)[^\\\/\s]+";

        protected override bool ShouldMaskInput(string input)
        {
            try
            {
                var current = Environment.UserName;
                return !string.IsNullOrEmpty(current) &&
                       string.Equals(input, current, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
