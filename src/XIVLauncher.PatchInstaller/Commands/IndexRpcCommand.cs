using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;

namespace XIVLauncher.PatchInstaller.Commands;

public class IndexRpcCommand
{
    public static readonly Command COMMAND = new("index-rpc") { IsHidden = true };

    private static readonly Argument<int> MonitorProcessIDArgument = new("process-id");

    private static readonly Argument<string> ChannelNameArgument = new("channel-name");

    static IndexRpcCommand()
    {
        COMMAND.AddArgument(MonitorProcessIDArgument);
        COMMAND.AddArgument(ChannelNameArgument);
        COMMAND.SetHandler(x => new IndexRpcCommand(x.ParseResult).Handle());
    }

    private readonly int monitorProcessId;
    private readonly string channelName;

    private IndexRpcCommand(ParseResult parseResult)
    {
        this.monitorProcessId = parseResult.GetValueForArgument(MonitorProcessIDArgument);
        this.channelName = parseResult.GetValueForArgument(ChannelNameArgument);
    }

    private async Task<int> Handle()
    {
        new IndexedZiPatchIndexRemoteInstaller.WorkerSubprocessBody(this.monitorProcessId, this.channelName).RunToDisposeSelf();
        return 0;
    }
}
