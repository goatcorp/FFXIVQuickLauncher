using System;
using System.IO;
using System.IO.Compression;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes.Implementations;

public class MacVideoFix : GameFix
{
    private const string MAC_ZIP_URL = "https://mac-dl.ffxiv.com/cw/finalfantasyxiv-1.0.7.zip";

    public MacVideoFix(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory, DirectoryInfo tempDirectory)
        : base(gameDirectory, configDirectory, winePrefixDirectory, tempDirectory)
    {
    }

    public override string LoadingTitle => "Preparing FMV cutscenes...";

    public override void Apply()
    {
        var outputDirectory = new DirectoryInfo(Path.Combine(GameDir.FullName, "game", "movie", "ffxiv"));
        var flagFile = new FileInfo(Path.Combine(outputDirectory.FullName, ".fixed"));

        if (flagFile.Exists)
            return;

        var zipFilePath = Path.Combine(TempDir.FullName, $"{Guid.NewGuid()}.zip");
        using var client = new HttpClientDownloadWithProgress(MAC_ZIP_URL, zipFilePath);
        client.ProgressChanged += (size, downloaded, percentage) =>
        {
            if (percentage != null && size != null)
            {
                this.UpdateProgress?.Invoke($"{LoadingTitle} ({ApiHelpers.BytesToString(downloaded)}/{ApiHelpers.BytesToString(size.Value)})", true, (float)(percentage.Value / 100f));
            }
        };

        client.Download().GetAwaiter().GetResult();

        var tempMacExtract = Path.Combine(TempDir.FullName, "xlcore-macTempExtract");
        ZipFile.ExtractToDirectory(zipFilePath, tempMacExtract);

        var videoDirectory = new DirectoryInfo(Path.Combine(tempMacExtract, "FINAL FANTASY XIV ONLINE.app", "Contents", "SharedSupport", "finalfantasyxiv", "support", "published_Final_Fantasy", "drive_c",
            "Program Files (x86)", "SquareEnix", "FINAL FANTASY XIV - A Realm Reborn", "game", "movie", "ffxiv"));

        var filesMoved = 0;

        foreach (FileInfo movieFile in videoDirectory.GetFiles("*.bk2"))
        {
            movieFile.MoveTo(Path.Combine(outputDirectory.FullName, movieFile.Name), true);
            filesMoved++;
        }

        if (filesMoved == 0)
            throw new Exception("Didn't copy any movies.");

        File.WriteAllText(flagFile.FullName, DateTimeOffset.Now.ToUnixTimeSeconds().ToString());

        Directory.Delete(tempMacExtract, true);
        File.Delete(zipFilePath);
    }
}