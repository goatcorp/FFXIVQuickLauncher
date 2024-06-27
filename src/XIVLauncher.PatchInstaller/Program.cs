using System;
using System.CommandLine;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using XIVLauncher.PatchInstaller.Commands;

namespace XIVLauncher.PatchInstaller;

public static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Fatal)
                     .WriteTo.Debug()
                     .MinimumLevel.Verbose()
                     .CreateLogger();

        var rc = new RootCommand();
        rc.AddCommand(CheckIntegrityCommand.Command);
        rc.AddCommand(InstallCommand.Command);
        rc.AddCommand(IndexCreateCommand.Command);
        rc.AddCommand(IndexCreateIntegrityCommand.Command);
        rc.AddCommand(IndexVerifyCommand.Command);
        rc.AddCommand(IndexRepairCommand.Command);
        rc.AddCommand(IndexUpdateCommand.Command);
        rc.AddCommand(IndexRpcCommand.Command);
        rc.AddCommand(IndexRpcTestCommand.Command);
        rc.AddCommand(RpcCommand.Command);

        var ret = -1;

        try
        {
            ret = await rc.InvokeAsync(args);
            Log.Information("Operation complete.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Operation failed.");
        }

        return ret;
    }
}
