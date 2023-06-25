using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes.Implementations;

public class MacVideoFix : GameFix
{
    private static async Task<string> GetLatestMacZipUrl()
    {
        const string sparkleFeedUrl = "https://mac-dl.ffxiv.com/cw/finalfantasy-mac.xml";
        const string fallbackUrl = "https://mac-dl.ffxiv.com/cw/finalfantasyxiv-1.1.2.zip";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var sparkleFeed = XDocument.Parse(await client.GetStringAsync(sparkleFeedUrl));
            var latestItem = sparkleFeed.Descendants("item").FirstOrDefault();
            var enclosureElement = latestItem?.Element("enclosure");
            var urlAttribute = enclosureElement?.Attribute("url");
            return urlAttribute!.Value;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to extract Mac Zip URL from Sparkle update feed, using static fallback");
            return fallbackUrl;
        }
    }

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
        using var client = new HttpClientDownloadWithProgress(GetLatestMacZipUrl().GetAwaiter().GetResult(), zipFilePath);
        client.ProgressChanged += (size, downloaded, percentage) =>
        {
            if (percentage != null && size != null)
            {
                this.UpdateProgress?.Invoke($"{LoadingTitle} ({ApiHelpers.BytesToString(downloaded)}/{ApiHelpers.BytesToString(size.Value)})", true, (float)(percentage.Value / 100f));
            }
        };

        client.Download().GetAwaiter().GetResult();

        var zipMovieFileNames = movieFileNames.Select(movie => Path.Combine("game", "movie", "ffxiv", movie));

        using (var archive = ZipFile.OpenRead(zipFilePath))
        {
            foreach (var entry in archive.Entries)
            {
                if (zipMovieFileNames.Any((fileName) => entry.FullName.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    var destinationPath = Path.Combine(outputDirectory.FullName, entry.Name);
                    if (!File.Exists(destinationPath))
                        entry.ExtractToFile(destinationPath);
                }
            }
        }

        File.Delete(zipFilePath);
    }
}