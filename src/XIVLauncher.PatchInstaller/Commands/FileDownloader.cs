using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Game.Exceptions;

namespace XIVLauncher.PatchInstaller.Commands;

public class FileDownloader
{
    private const int BufferSize = 65536;
    private const int ConnectionDelay = 200;

    private const int SpeedBucketCount = 50;
    private const int SpeedBucketDuration = 100;
    private readonly long[] speedBucketBaseTick = new long[SpeedBucketCount];
    private readonly long[] speedAccumulator = new long[SpeedBucketCount];

    private readonly HttpClient client;
    private readonly string localPath;
    private readonly string? sid;
    private readonly CancellationToken cancellationToken;
    private readonly int numThreads;
    private readonly List<Fragment> fragments = new();
    private readonly Channel<Tuple<long, int, byte[]>> fileChannel;
    private string url;

    private Stream? localFile;

    public FileDownloader(HttpClient client, string url, string localPath, string? sid, CancellationToken cancellationToken, int numThreads)
    {
        this.client = client;
        this.url = url;
        this.localPath = localPath;
        this.sid = sid;
        this.cancellationToken = cancellationToken;
        this.numThreads = numThreads;
        this.fileChannel = Channel.CreateBounded<Tuple<long, int, byte[]>>(numThreads * 16);
    }

    public long TotalLength { get; private set; } = -1;
    public long DownloadedLength { get; private set; }
    public long BytesPerSecond => this.speedAccumulator.Sum() * 1000 / (SpeedBucketCount * SpeedBucketDuration);

    private async Task<HttpResponseMessage> GetResponseAsync(long start, long end)
    {
        if (start == 0 && end == 0)
            Log.Verbose("Connecting: {url}", this.url);
        else
            Log.Verbose("Connecting: {url} ({start:##,###}-{end:##,###})", this.url, start, end);

        using var req = new HttpRequestMessage(HttpMethod.Get, this.url);
        req.Headers.Add("User-Agent", Constants.PatcherUserAgent);
        req.Headers.Add("Connection", "Keep-Alive");
        if (start != 0 || end != 0)
            req.Headers.Range = new(start == 0 ? null : start, end == 0 ? null : end);
        if (this.sid != null)
            req.Headers.Add("X-Patch-Unique-Id", this.sid);
        // Note: "req" has to be alive during the await, so we async+return await instead of plain return.
        return await this.client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, this.cancellationToken);
    }

    public async Task Download()
    {
        var tempPath = $"{this.localPath}.tmp.{Process.GetCurrentProcess().Id:X08}{Environment.TickCount:X16}0000";
        for (var i = 1; File.Exists(tempPath); i++)
            tempPath = $"{this.localPath}.tmp.{Process.GetCurrentProcess().Id:X08}{Environment.TickCount:X16}{i:X04}";

        this.localFile = File.Create(tempPath);
        var flushTask = this.FlushTask();

        try
        {
            try
            {
                HttpResponseMessage? response = null;
                Stream? stream = null;

                while (true)
                {
                    try
                    {
                        response = await this.GetResponseAsync(0, 0);
                        stream = await response.Content.ReadAsStreamAsync();

                        switch (response.StatusCode)
                        {
                            case HttpStatusCode.OK:
                                break;

                            case HttpStatusCode.Redirect:
                            case HttpStatusCode.TemporaryRedirect:
                                this.url = response.Headers.Location.ToString();
                                continue;

                            default:
                                throw new InvalidResponseException("Invalid response status code", response.StatusCode.ToString());
                        }

                        if (response.Content.Headers.ContentLength is not { } length)
                        {
                            Log.Information("File size unknown");

                            await stream.CopyToAsync(this.localFile);
                            this.localFile?.Dispose();
                            this.localFile = null;
                            File.Move(tempPath, this.localPath);
                            return;
                        }

                        this.TotalLength = length;
                        Log.Information("Downloading {length:##,###} bytes", length);
                        this.fragments.Add(new(this, response, stream, 0, length));
                        this.fragments.Add(new(this, null, null, length, length));
                        response = null;
                        stream = null;

                        break;
                    } finally
                    {
                        response?.Dispose();
                        stream?.Dispose();
                        response = null;
                        stream = null;
                    }
                }

                var working = new List<Task>();

                while (await this.MergeAndFindGap() != -1)
                {
                    working.Clear();
                    working.AddRange(this.fragments.Select(x => x.DownloadTask).Where(x => !x.IsCompleted));

                    if (working.Count >= this.numThreads)
                    {
                        await Task.WhenAny(working.Append(Task.Delay(200, this.cancellationToken)));
                        _ = await this.MergeAndFindGap();
                        continue;
                    }

                    await Task.Delay(ConnectionDelay, this.cancellationToken);
                    var largestGap = await this.MergeAndFindGap();
                    if (largestGap == -1)
                        break;

                    var cur = this.fragments[largestGap];
                    var next = this.fragments[largestGap + 1];

                    var fragStart = cur.DownloadTask.IsCompleted ? cur.AvailEnd : (cur.AvailEnd + next.Start) / 2;
                    var fragEnd = next.Start;
                    if (fragStart >= fragEnd)
                        continue;

                    try
                    {
                        response = await this.GetResponseAsync(fragStart, fragEnd);
                        stream = await response.Content.ReadAsStreamAsync();

                        if (response.StatusCode != HttpStatusCode.PartialContent)
                            throw new InvalidResponseException($"Invalid response status code: {response.StatusCode}", "");

                        this.fragments[largestGap].MaxEnd = fragStart;
                        this.fragments.Insert(largestGap + 1, new(this, response, stream, fragStart, fragEnd));
                        response = null;
                        stream = null;
                    } finally
                    {
                        response?.Dispose();
                        stream?.Dispose();
                        response = null;
                        stream = null;
                    }
                }
            } finally
            {
                foreach (var f in this.fragments)
                    await f.DisposeAsync();

                this.fileChannel.Writer.Complete();
                await flushTask;
            }

            this.localFile?.Dispose();
            this.localFile = null;
            File.Move(tempPath, this.localPath);
        }
        catch (Exception)
        {
            this.localFile?.Dispose();
            this.localFile = null;

            try
            {
                File.Delete(tempPath);
            }
            catch (Exception)
            {
                // ignore
            }

            throw;
        } finally
        {
            foreach (var f in this.fragments)
                await f.DisposeAsync();
            this.fragments.Clear();
        }
    }

    private async Task FlushTask()
    {
        try
        {
            while (true)
            {
                var (from, size, buffer) = await this.fileChannel.Reader.ReadAsync(this.cancellationToken);
                this.localFile!.Seek(from, SeekOrigin.Begin);
                await this.localFile.WriteAsync(buffer, 0, size, this.cancellationToken);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (ChannelClosedException)
        {
            // ignore
        }
    }

    private async Task<int> MergeAndFindGap()
    {
        var largestGap = -1;

        for (var i = 0; i < this.fragments.Count - 1; i++)
        {
            var cur = this.fragments[i];
            var next = this.fragments[i + 1];

            // both are finished and continuous then merge
            if (cur.AvailEnd >= next.Start)
            {
                if (cur.DownloadTask.IsCompleted && next.DownloadTask.IsCompleted)
                {
                    this.fragments[i] = new(this, null, null, cur.Start, next.AvailEnd);
                    this.fragments.RemoveAt(i + 1);
                    await cur.DisposeAsync();
                    await next.DisposeAsync();
                    i--;
                    continue;
                }

                await cur.DisposeAsync();
            }

            if (largestGap == -1)
            {
                largestGap = i;
            }
            else
            {
                var prevGap = this.fragments[largestGap + 1].Start - this.fragments[largestGap].AvailEnd;
                var currGap = next.Start - cur.AvailEnd;
                if ((cur.DownloadTask.IsCompleted ? currGap : currGap / 2) > prevGap)
                    largestGap = i;
            }
        }

        this.DownloadedLength = Math.Min(
            this.fragments.Sum(x => x.AvailEnd - x.Start),
            this.TotalLength == 0 ? long.MaxValue : this.TotalLength);

        return largestGap;
    }

    private async Task Write(long from, int size, byte[] buffer)
    {
        var baseTick = Environment.TickCount / SpeedBucketDuration;
        var speedBucket = baseTick % SpeedBucketCount;

        if (this.speedBucketBaseTick[speedBucket] != baseTick)
        {
            this.speedBucketBaseTick[speedBucket] = baseTick;
            this.speedAccumulator[speedBucket] = size;
        }
        else
        {
            this.speedAccumulator[speedBucket] += size;
        }

        await this.fileChannel.Writer.WriteAsync(Tuple.Create(from, size, buffer), this.cancellationToken);
    }

    private sealed class Fragment : IDisposable, IAsyncDisposable
    {
        public readonly long Start;
        public readonly Task DownloadTask;

        public long MaxEnd;
        public long AvailEnd;

        private readonly HttpResponseMessage? httpResponseMessage;
        private readonly Stream? stream;
        private CancellationTokenSource? cancellationTokenSource;
        private readonly FileDownloader parent;

        public Fragment(FileDownloader parent, HttpResponseMessage? httpResponseMessage, Stream? stream, long start, long maxEnd)
        {
            this.parent = parent;
            this.httpResponseMessage = httpResponseMessage;
            this.stream = stream;
            this.cancellationTokenSource = stream is null ? null : new();

            this.Start = start;
            this.MaxEnd = maxEnd;
            this.AvailEnd = stream is null ? maxEnd : start;
            this.DownloadTask = stream is null ? Task.CompletedTask : Task.Run(this.TaskBody);
        }

        public void Dispose()
        {
            this.cancellationTokenSource?.Cancel();
            this.DownloadTask.Wait();
            this.cancellationTokenSource?.Dispose();
            this.cancellationTokenSource = null;
        }

        public async ValueTask DisposeAsync()
        {
            this.cancellationTokenSource?.Cancel();

            try
            {
                await this.DownloadTask;
            }
            catch (Exception)
            {
                // ignore
            }

            this.cancellationTokenSource?.Dispose();
            this.cancellationTokenSource = null;
        }

        private async Task TaskBody()
        {
            using var _1 = this.httpResponseMessage!;
            using var _2 = this.stream!;
            var token = this.cancellationTokenSource!.Token;

            while (true)
            {
                var avail = unchecked((int)Math.Min(BufferSize, this.MaxEnd - this.AvailEnd));
                if (avail <= 0)
                    break;

                var buf = ArrayPool<byte>.Shared.Rent(avail);
                avail = await this.stream!.ReadAsync(buf, 0, avail, token);

                if (avail == 0)
                {
                    ArrayPool<byte>.Shared.Return(buf);
                    break;
                }

                await this.parent.Write(this.AvailEnd, avail, buf);
                this.AvailEnd += avail;
            }
        }
    }
}
