using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common.Patching;

namespace XIVLauncher.PatchInstaller.Commands;

public class InstallCommand
{
    public static readonly Command Command = new("install", "Install the given patch files in the specified order.");

    private static readonly Argument<string> GameRootPathArgument = new(
        "game-root",
        "Path to a game installation, such as \"C:\\Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn\\\"");

    private static readonly Argument<string[]> PatchFilesArgument = new("patch-file", "Path to patch file(s).")
    {
        Arity = ArgumentArity.OneOrMore,
    };

    static InstallCommand()
    {
        Command.AddArgument(GameRootPathArgument);
        Command.AddArgument(PatchFilesArgument);
        Command.SetHandler(x => new InstallCommand(x.ParseResult).Handle());
    }

    private readonly string gameRootPath;
    private readonly string[] patchFiles;

    private InstallCommand(ParseResult parseResult)
    {
        this.gameRootPath = parseResult.GetValueForArgument(GameRootPathArgument);
        this.patchFiles = parseResult.GetValueForArgument(PatchFilesArgument);

        // Do we have a .patch as the first argument?
        if (File.Exists(this.gameRootPath) && this.gameRootPath.EndsWith(".patch", StringComparison.OrdinalIgnoreCase))
        {
            var lastArg = this.patchFiles[this.patchFiles.Length - 1];

            // Do we have a folder as the last argument?
            if (Directory.Exists(lastArg) || lastArg.EndsWith("/", StringComparison.Ordinal) || lastArg.EndsWith("\\", StringComparison.Ordinal))
            {
                Log.Information("Taking the first argument as the first patch file, and the last argument as the target directory.");
                this.patchFiles = new[] { this.gameRootPath }.Concat(this.patchFiles.Take(this.patchFiles.Length - 1)).ToArray();
                this.gameRootPath = lastArg;
            }
        }
    }

    private Task<int> Handle()
    {
        foreach (var file in this.patchFiles)
        {
            var fi = new FileInfo(file);
            if (!fi.Exists)
                throw new FileNotFoundException("File not found", file);
            if (fi.Length == 0)
                throw new FileFormatException($"File is empty: {file}");
        }

        foreach (var file in this.patchFiles)
            RemotePatchInstaller.InstallPatch(file, this.gameRootPath);
        return Task.FromResult(0);
    }
}
