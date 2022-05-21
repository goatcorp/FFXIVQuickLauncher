using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Util;

public class HttpClientDownloadWithProgress : IDisposable
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

    public async Task Download(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromDays(1);
        this.httpClient = new HttpClient { Timeout = timeout.Value };

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