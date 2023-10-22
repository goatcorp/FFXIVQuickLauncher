using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using XIVLauncher.Common.Patching;
using XIVLauncher.Common.Patching.Rpc.Implementations;

namespace XIVLauncher.PatchInstaller.Commands;

public class RpcCommand
{
    public static readonly Command COMMAND = new("rpc") { IsHidden = true };

    private static readonly Argument<string> ChannelNameArgument = new("channel-name");

    static RpcCommand()
    {
        COMMAND.AddArgument(ChannelNameArgument);
        COMMAND.SetHandler(x => new RpcCommand(x.ParseResult).Handle());
    }

    private readonly string channelName;

    private RpcCommand(ParseResult parseResult)
    {
        this.channelName = parseResult.GetValueForArgument(ChannelNameArgument);
    }

    private async Task<int> Handle()
    {
        try
        {
            var installer = new RemotePatchInstaller(new SharedMemoryRpc(this.channelName));
            installer.Start();

            while (true)
            {
                if ((Process.GetProcesses().All(x => x.ProcessName != "XIVLauncher") && !installer.HasQueuedInstalls) || installer.IsDone)
                {
                    Environment.Exit(0);
                    return 0; // does not run
                }

                Thread.Sleep(1000);

                if (installer.IsFailed)
                {
                    Log.Information("Exited due to failure");
                    Environment.Exit(-1);
                    return -1; // does not run
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
