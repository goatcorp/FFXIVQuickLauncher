using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using Serilog.Events;
using XIVLauncher.Common;
using XIVLauncher.Common.Patching;
using XIVLauncher.Common.Patching.Rpc.Implementations;

namespace XIVLauncher.PatchInstaller.Commands;

public class RpcCommand
{
    public static readonly Command Command = new("rpc") { IsHidden = true };

    private static readonly Argument<string> ChannelNameArgument = new("channel-name");

    static RpcCommand()
    {
        Command.AddArgument(ChannelNameArgument);
        Command.SetHandler(x => new RpcCommand(x.ParseResult).Handle());
    }

    private readonly string channelName;

    private RpcCommand(ParseResult parseResult)
    {
        this.channelName = parseResult.GetValueForArgument(ChannelNameArgument);
    }

    private Task<int> Handle()
    {
        Log.Logger = new LoggerConfiguration()
                     .WriteTo.Console(standardErrorFromLevel: LogEventLevel.Fatal)
                     .WriteTo.File(Path.Combine(Paths.RoamingPath, "patcher.log"))
                     .WriteTo.Debug()
                     .MinimumLevel.Verbose()
                     .CreateLogger();

        try
        {
            var installer = new RemotePatchInstaller(new SharedMemoryRpc(this.channelName));
            installer.Start();

            while (true)
            {
                if ((Process.GetProcesses().All(x => x.ProcessName != "XIVLauncher") && !installer.HasQueuedInstalls) || installer.IsDone)
                {
                    Environment.Exit(0);
                    return Task.FromResult(0); // does not run
                }

                Thread.Sleep(1000);

                if (installer.IsFailed)
                {
                    Log.Information("Exited due to failure");
                    Environment.Exit(-1);
                    return Task.FromResult(-1); // does not run
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Patcher init failed.\n\n" + ex, "XIVLauncher", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }
}
