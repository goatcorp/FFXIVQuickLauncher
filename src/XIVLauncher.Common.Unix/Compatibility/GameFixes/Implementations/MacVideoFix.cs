using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Unix.Compatibility.GameFixes.Implementations;

public class MacVideoFix : GameFix
{
    private const string MAC_ZIP_URL = "https://mac-dl.ffxiv.com/cw/finalfantasyxiv-1.0.7.zip";

    public MacVideoFix(DirectoryInfo gameDirectory, DirectoryInfo configDirectory, DirectoryInfo winePrefixDirectory)
        : base(gameDirectory, configDirectory, winePrefixDirectory)
    {
    }

    public override string LoadingTitle => "Preparing FMV cutscenes...";

    public override void Apply()
    {
        var outputDirectory = new DirectoryInfo(Path.Combine(GameDir.FullName, "game", "movie", "ffxiv"));
        var flagFile = new FileInfo(Path.Combine(outputDirectory.FullName, ".fixed"));

        if (flagFile.Exists)
            return;

        var zipFilePath = Path.GetTempFileName();
        using var client = new HttpClientDownloadWithProgress(MAC_ZIP_URL, zipFilePath);
        client.ProgressChanged += (size, downloaded, percentage) =>
        {
            if (percentage != null)
            {
                this.UpdateProgress?.Invoke(LoadingTitle, true, (float)percentage.Value);
            }
        };

        client.StartDownload().GetAwaiter().GetResult();

        var tempMacExtract = Path.Combine(Path.GetTempPath(), "xlcore-macTempExtract");
        Util.Unzip(zipFilePath, tempMacExtract);

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
    }

    private class HttpClientDownloadWithProgress : IDisposable
    {
        private readonly string downloadUrl;
        private readonly string destinationFilePath;

        private HttpClient httpClient;

        public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);

        public event ProgressChangedHandler ProgressChanged;

        public HttpClientDownloadWithProgress(string downloadUrl, string destinationFilePath)
        {
            this.downloadUrl = downloadUrl;
            this.destinationFilePath = destinationFilePath;
        }

        public async Task StartDownload()
        {
            this.httpClient = new HttpClient { Timeout = TimeSpan.FromDays(1) };

            using var response = await this.httpClient.GetAsync(this.downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            await this.DownloadFileFromHttpResponseMessage(response).ConfigureAwait(false);
        }

        private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await this.ProcessContentStream(totalBytes, contentStream).ConfigureAwait(false);
        }

        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream)
        {
            var totalBytesRead = 0L;
            var readCount = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            using var fileStream = new FileStream(this.destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            do
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    isMoreToRead = false;
                    this.TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                    continue;
                }

                await fileStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);

                totalBytesRead += bytesRead;
                readCount += 1;

                if (readCount % 100 == 0)
                    this.TriggerProgressChanged(totalDownloadSize, totalBytesRead);
            } while (isMoreToRead);
        }

        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
        {
            if (this.ProgressChanged == null)
                return;

            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
                progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

            this.ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
        }

        public void Dispose()
        {
            this.httpClient?.Dispose();
        }
    }
}