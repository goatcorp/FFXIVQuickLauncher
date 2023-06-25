using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes.Implementations;

public class MacVideoFix : GameFix
{
    private const string MAC_ZIP_URL = "https://mac-dl.ffxiv.com/cw/finalfantasyxiv-1.1.2.zip";

    public MacVideoFix(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory, DirectoryInfo tempDirectory)
        : base(gameDirectory, configDirectory, winePrefixDirectory, tempDirectory)
    {
    }

    public override string LoadingTitle => "Preparing FMV cutscenes...";

    public override void Apply()
    {
        var outputDirectory = new DirectoryInfo(Path.Combine(GameDir.FullName, "game", "movie", "ffxiv"));
        var movieFileNames = new [] { "00000.bk2", "00001.bk2", "00002.bk2", "00003.bk2" };
        var movieFiles = movieFileNames.Select(movie => new FileInfo(Path.Combine(outputDirectory.FullName, movie)));

        if (movieFiles.All((movieFile) => movieFile.Exists))
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

        var zipMovieFileNames = movieFileNames.Select(movie => Path.Combine("game", "movie", "ffxiv", movie));

        using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (zipMovieFileNames.Any((fileName) => entry.FullName.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    string destinationPath = Path.Combine(outputDirectory.FullName, entry.Name);
                    if (!File.Exists(destinationPath))
                        entry.ExtractToFile(destinationPath);
                }
            }
        }

        File.Delete(zipFilePath);
    }
}