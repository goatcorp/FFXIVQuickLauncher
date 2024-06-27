using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using XIVLauncher.Common;
using XIVLauncher.Common.Patching.IndexedZiPatch;

namespace XIVLauncher.PatchInstaller.Commands;

public class IndexRpcCommand
{
    public static readonly Command Command = new("index-rpc") { IsHidden = true };

    private static readonly Argument<int> MonitorProcessIDArgument = new("process-id");

    private static readonly Argument<string> ChannelNameArgument = new("channel-name");

    static IndexRpcCommand()
    {
        Command.AddArgument(MonitorProcessIDArgument);
        Command.AddArgument(ChannelNameArgument);
        Command.SetHandler(x => new IndexRpcCommand(x.ParseResult).Handle());
    }

    private readonly int monitorProcessId;
    private readonly string channelName;

    private IndexRpcCommand(ParseResult parseResult)
    {
        this.monitorProcessId = parseResult.GetValueForArgument(MonitorProcessIDArgument);
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

        new IndexedZiPatchIndexRemoteInstaller.WorkerSubprocessBody(this.monitorProcessId, this.channelName).RunToDisposeSelf();
        return Task.FromResult(0);
    }
}
