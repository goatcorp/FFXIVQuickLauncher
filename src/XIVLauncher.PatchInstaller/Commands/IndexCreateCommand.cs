using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.IndexedZiPatch;

namespace XIVLauncher.PatchInstaller.Commands;

public class IndexCreateCommand
{
    public static readonly Command Command = new("index-create", "Create patch index files according to a patch chain specified from arguments.");

    private static readonly Argument<int> ExpacVersionArgument = new("expac-version", "Expansion pack version in an integer. -1 = boot, 0 = base game, 1 = Heavensward, etc.");

    private static readonly Argument<string[]> PatchFilesArgument = new("patch-file", "Path to patch file(s).")
    {
        Arity = ArgumentArity.OneOrMore,
    };

    static IndexCreateCommand()
    {
        Command.AddArgument(ExpacVersionArgument);
        Command.AddArgument(PatchFilesArgument);
        Command.SetHandler(x => new IndexCreateCommand(x.ParseResult).Handle());
    }

    private readonly int expacVersion;
    private readonly string[] patchFiles;

    private IndexCreateCommand(ParseResult parseResult)
    {
        this.expacVersion = parseResult.GetValueForArgument(ExpacVersionArgument);
        this.patchFiles = parseResult.GetValueForArgument(PatchFilesArgument);
    }

    private async Task<int> Handle()
    {
        await IndexedZiPatchOperations.CreateZiPatchIndices(this.expacVersion, this.patchFiles);
        return 0;
    }
}
