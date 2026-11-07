using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Util;

public class HttpClientDownloadWithProgress
{
    private readonly string downloadUrl;
    private readonly string destinationFilePath;
    private readonly HttpClient httpClient;

    public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);

    public event ProgressChangedHandler? ProgressChanged;

    public HttpClientDownloadWithProgress(HttpClient client, string downloadUrl, string destinationFilePath)
    {
        this.downloadUrl = downloadUrl;
        this.destinationFilePath = destinationFilePath;

        this.httpClient = client;
    }

    // Max time without receiving bytes
    private static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(60);

    public async Task Download(TimeSpan? idleTimeout = null)
    {
        var timeout = idleTimeout ?? DefaultIdleTimeout;
        using var idleCts = new CancellationTokenSource(timeout);

        try
        {
            using var response = await this.httpClient.GetAsync(this.downloadUrl, HttpCompletionOption.ResponseHeadersRead, idleCts.Token).ConfigureAwait(false);
            await this.DownloadFileFromHttpResponseMessage(response, idleCts, timeout).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (idleCts.IsCancellationRequested)
        {
            throw new TimeoutException($"Download stalled for {timeout.TotalSeconds}s: {this.downloadUrl}");
        }
    }

    private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response, CancellationTokenSource idleCts, TimeSpan idleTimeout)
    {
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        using var contentStream = await response.Content.ReadAsStreamAsync(idleCts.Token).ConfigureAwait(false);
        await this.ProcessContentStream(totalBytes, contentStream, idleCts, idleTimeout).ConfigureAwait(false);
    }

    private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream, CancellationTokenSource idleCts, TimeSpan idleTimeout)
    {
        var totalBytesRead = 0L;
        var readCount = 0L;
        var buffer = new byte[8192];
        var isMoreToRead = true;

        using var fileStream = new FileStream(this.destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        do
        {
            idleCts.CancelAfter(idleTimeout);
            var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, idleCts.Token).ConfigureAwait(false);

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
        }
        while (isMoreToRead);

        if (totalDownloadSize.HasValue && totalBytesRead != totalDownloadSize.Value)
        {
            throw new IOException($"Download incomplete, got {totalBytesRead}/{totalDownloadSize.Value} bytes: {this.downloadUrl}");
        }
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
}
