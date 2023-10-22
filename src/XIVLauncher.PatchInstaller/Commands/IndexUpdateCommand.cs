using System;
using System.Buffers;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Exceptions;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.Game.Patch.PatchList;
using XIVLauncher.Common.Patching.IndexedZiPatch;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Util;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.PlatformAbstractions;

namespace XIVLauncher.PatchInstaller.Commands;

public class IndexUpdateCommand
{
    public static readonly Command COMMAND = new("index-update", "Update patch index files from internet.");

    private static readonly Option<string?> PatchRootPathOption = new(
        "-r",
        () => null,
        "Root directory of patch file hierarchy. Defaults to a directory under the temp directory of the current user.");

    private static readonly Option<string?> UserNameOption = new("-u", () => null, "User ID.");
    private static readonly Option<string?> PasswordOption = new("-p", () => null, "User password.");
    private static readonly Option<string?> OtpOption = new("-o", () => null, "User OTP.");

    static IndexUpdateCommand()
    {
        COMMAND.AddOption(PatchRootPathOption);
        COMMAND.AddOption(UserNameOption);
        COMMAND.AddOption(PasswordOption);
        COMMAND.AddOption(OtpOption);
        COMMAND.SetHandler(x => new IndexUpdateCommand(x.ParseResult).Handle(x.GetCancellationToken()));
    }

    private readonly TempSettings settings;
    private readonly string? username;
    private readonly string? password;
    private readonly string? otp;

    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        UseCookies = false,
        MaxConnectionsPerServer = 65535,
    });

    private IndexUpdateCommand(ParseResult parseResult)
    {
        this.settings = new(
            new(parseResult.GetValueForOption(PatchRootPathOption)
                ?? Path.Combine(Path.GetTempPath(), "XIVLauncher.PatchInstaller")));
        this.username = parseResult.GetValueForOption(UserNameOption);
        this.password = parseResult.GetValueForOption(PasswordOption);
        this.otp = parseResult.GetValueForOption(OtpOption);
    }

    private async Task<int> Handle(CancellationToken cancellationToken)
    {
        var la = new Launcher((ISteam?)null, new CommonUniqueIdCache(null), this.settings);

        var bootPatchListFilePath = Path.Combine(this.settings.GamePath.FullName, "bootlist.json");

        if (!TryReadPatchListEntries(bootPatchListFilePath, out var bootPatchList))
        {
            Log.Information("Downloading boot patch information.");
            bootPatchList = await la.CheckBootVersion(this.settings.PatchPath, true);
            File.WriteAllText(bootPatchListFilePath, JsonConvert.SerializeObject(bootPatchList, Formatting.Indented));
        }

        using (var zpStore = new SqexFileStreamStore())
        {
            var zpConfig = new ZiPatchConfig(Path.Combine(this.settings.GamePath.FullName, "boot")) { Store = zpStore };
            var bootVerPath = Path.Combine(zpConfig.GamePath, "ffxivboot.ver");
            var bootBckPath = Path.Combine(zpConfig.GamePath, "ffxivboot.bck");
            var bootVerExpected = Path.GetFileNameWithoutExtension(bootPatchList.Last().Url).Substring(1);

            if (!File.Exists(bootVerPath) || !File.Exists(bootBckPath) || File.ReadAllText(bootVerPath) != bootVerExpected || File.ReadAllText(bootBckPath) != bootVerExpected)
            {
                foreach (var i in Enumerable.Range(0, bootPatchList.Length))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var patch = bootPatchList[i];
                    var uri = new Uri(patch.Url);
                    var localPath = new FileInfo(Path.Combine(this.settings.GamePath.FullName, EnsureRelativePath(uri.LocalPath)));

                    if (!localPath.Exists || localPath.Length != patch.Length)
                    {
                        Log.Information("Downloading patch [{index}/{total}]: {path}", i + 1, bootPatchList.Length, patch.Url);
                        var fd = new FileDownloader(Client, patch.Url, localPath.FullName, null, cancellationToken, 8);
                        var dtask = fd.Download();

                        while (!dtask.IsCompleted)
                        {
                            await Task.WhenAny(dtask, Task.Delay(200, cancellationToken));
                            Log.Information(
                                "Downloaded {curr:##,###} bytes out of {total:##,###} bytes ({percentage:F2}%): {speed:##,###}b/s",
                                fd.DownloadedLength,
                                fd.TotalLength,
                                fd.TotalLength == 0 ? 0 : fd.DownloadedLength * 100 / fd.TotalLength,
                                fd.BytesPerSecond);
                        }
                    }

                    Log.Information("Applying patch [{index}/{total}]: {path}", i + 1, bootPatchList.Length, Path.GetFileName(patch.Url));

                    using var ziPatch = new ZiPatchFile(localPath.OpenRead());
                    foreach (var chunk in ziPatch.GetChunks())
                        chunk.ApplyChunk(zpConfig);
                }

                File.WriteAllText(bootVerPath, bootVerExpected);
                File.WriteAllText(Path.ChangeExtension(bootVerPath, ".bck"), bootVerExpected);
            }
        }

        var gamePatchListFilePath = Path.Combine(this.settings.GamePath.FullName, "gamelist.json");
        PatchListEntry[] gamePatchList;

        if (this.username is not null && this.password is not null)
        {
            Log.Information("Logging in and fetching game patch information.");
            var lr = await la.Login(this.username, this.password, this.otp ?? "", false, false, this.settings.GamePath, true, false);
            gamePatchList = lr.PendingPatches;
            File.WriteAllText(gamePatchListFilePath, JsonConvert.SerializeObject(gamePatchList, Formatting.Indented));
        }
        else if (!TryReadPatchListEntries(gamePatchListFilePath, out gamePatchList))
        {
            Log.Error("No previous game patch file list found. You need to log in.");
            return -1;
        }

        var indexSources = gamePatchList.GroupBy(x => x.GetRepoName() switch
        {
            "ffxiv" => 0,
            var y => int.Parse(y.Substring(2)),
        }).ToDictionary(x => x.Key, x => x.ToArray());
        indexSources[-1] = bootPatchList;

        var fileCompletions = gamePatchList.ToDictionary(x => x, _ => new TaskCompletionSource<PatchListEntry>());

        await Task.WhenAll(
            Task.Run(async () =>
            {
                foreach (var i in Enumerable.Range(0, gamePatchList.Length))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var patch = gamePatchList[i];
                    var uri = new Uri(patch.Url);
                    var localPath = new FileInfo(Path.Combine(this.settings.GamePath.FullName, EnsureRelativePath(uri.LocalPath)));

                    if (patch.HashType != "sha1")
                        throw new NotSupportedException($"HashType \"{patch.HashType}\" is not supported for: {uri}");

                    var downloadRequired = !localPath.Exists || localPath.Length != patch.Length;

                    if (!downloadRequired)
                    {
                        Log.Information("Verifying patch [{index}/{total}]: {path}", i + 1, gamePatchList.Length, patch.Url);
                        downloadRequired = !await CheckPatchHashAsync(localPath, patch, cancellationToken);
                    }

                    if (downloadRequired)
                    {
                        Log.Information("Downloading patch [{index}/{total}]: {path}", i + 1, gamePatchList.Length, patch.Url);
                        var fd = new FileDownloader(Client, patch.Url, localPath.FullName, null, cancellationToken, 8);
                        var dtask = fd.Download();

                        while (!dtask.IsCompleted)
                        {
                            await Task.WhenAny(dtask, Task.Delay(200, new()));
                            Log.Information(
                                "Downloaded {curr:##,###} bytes out of {total:##,###} bytes ({percentage:F2}%): {speed:##,###}b/s",
                                fd.DownloadedLength,
                                fd.TotalLength,
                                fd.TotalLength == 0 ? 0 : fd.DownloadedLength * 100 / fd.TotalLength,
                                fd.BytesPerSecond);
                        }

                        // propagate exception if any happened
                        await dtask;

                        if (!await CheckPatchHashAsync(localPath, patch, cancellationToken))
                            throw new IOException("Downloaded file did not pass hash check");
                    }

                    fileCompletions[patch].SetResult(patch);
                }
            }, cancellationToken),
            Task.Run(async () =>
            {
                foreach (var (expac, patches) in indexSources.Select(x => (x.Key, x.Value)))
                {
                    var patchFilePaths = patches.Select(x => Path.Combine(this.settings.GamePath.FullName, EnsureRelativePath(new Uri(x.Url).LocalPath))).ToList();
                    var firstPatchFileIndex = patchFilePaths.Count - 1;
                    IndexedZiPatchIndex? patchIndex = null;

                    for (; firstPatchFileIndex >= 0; firstPatchFileIndex--)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var indexPath = patchFilePaths[firstPatchFileIndex] + ".index";
                        if (!File.Exists(indexPath))
                            continue;

                        patchIndex = new(new BinaryReader(new DeflateStream(new FileStream(indexPath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));
                        if (patchIndex.ExpacVersion != expac)
                            continue;

                        var i = 0;

                        for (; i < patchIndex.Sources.Count && i < firstPatchFileIndex; i++)
                        {
                            if (patchIndex.Sources[i] != Path.GetFileName(patchFilePaths[i]))
                                break;
                        }

                        if (i == firstPatchFileIndex)
                            break;

                        firstPatchFileIndex = i;
                    }

                    ++firstPatchFileIndex;

                    var sources = new List<Stream>();
                    var patchFiles = new List<ZiPatchFile>();
                    patchIndex ??= new(expac);

                    try
                    {
                        for (var i = 0; i < patchFilePaths.Count; ++i)
                        {
                            await Task.WhenAny(fileCompletions[patches[i]].Task, Task.Delay(int.MaxValue, cancellationToken));
                            cancellationToken.ThrowIfCancellationRequested();

                            var patchFilePath = patchFilePaths[i];
                            sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));
                            patchFiles.Add(new(sources[sources.Count - 1]));

                            if (i < firstPatchFileIndex)
                                continue;

                            Log.Information("Indexing patch [ex{expac}: {index}/{total}]: {file}", expac, i + 1, patchFilePath.Length, patchFilePath);
                            await patchIndex.ApplyZiPatch(Path.GetFileName(patchFilePath), patchFiles[patchFiles.Count - 1], cancellationToken);

                            Log.Information("Hashing indexed patch [ex{expac}: {index}/{total}]: {file}.index", expac, i + 1, patchFilePath.Length, patchFilePath);
                            await patchIndex.CalculateCrc32(sources, cancellationToken);

                            using (var writer = new BinaryWriter(new DeflateStream(new FileStream(patchFilePath + ".index.tmp", FileMode.Create), CompressionLevel.Optimal)))
                                patchIndex.WriteTo(writer);

                            File.Move(patchFilePath + ".index.tmp", patchFilePath + ".index");
                        }
                    } finally
                    {
                        foreach (var source in sources)
                            source.Dispose();
                    }
                }
            }, cancellationToken));
        return 0;
    }

    private class FileDownloader
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

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", Constants.PatcherUserAgent);
            req.Headers.Add("Connection", "Keep-Alive");
            if (start != 0 || end != 0)
                req.Headers.Range = new(start == 0 ? null : start, end == 0 ? null : end);
            if (sid != null)
                req.Headers.Add("X-Patch-Unique-Id", sid);
            // Note: "req" has to be alive during the await, so we async+return await instead of plain return.
            return await this.client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        public async Task Download()
        {
            var tempPath = $"{this.localPath}.tmp.{Process.GetCurrentProcess().Id:X08}{Environment.TickCount:X16}0000";
            for (var i = 1; File.Exists(tempPath); i++)
                tempPath = $"{this.localPath}.tmp.{Process.GetCurrentProcess().Id:X08}{Environment.TickCount:X16}{i:X04}";

            this.localFile = File.Create(tempPath);
            var flushTask = FlushTask();

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
                                    url = response.Headers.Location.ToString();
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

                            TotalLength = length;
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

                    while (await MergeAndFindGap() != -1)
                    {
                        working.Clear();
                        working.AddRange(this.fragments.Select(x => x.DownloadTask).Where(x => !x.IsCompleted));

                        if (working.Count >= this.numThreads)
                        {
                            await Task.WhenAny(working.Append(Task.Delay(200, this.cancellationToken)));
                            _ = await MergeAndFindGap();
                            continue;
                        }

                        await Task.Delay(ConnectionDelay, this.cancellationToken);
                        var largestGap = await MergeAndFindGap();
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

            await fileChannel.Writer.WriteAsync(Tuple.Create(from, size, buffer), this.cancellationToken);
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
                this.DownloadTask = stream is null ? Task.CompletedTask : Task.Run(TaskBody);
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
                    avail = await stream!.ReadAsync(buf, 0, avail, token);

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

    private static async Task<bool> CheckPatchHashAsync(FileInfo localPath, PatchListEntry patch, CancellationToken cancellationToken)
    {
        using var sha1 = SHA1.Create();
        using var f = localPath.OpenRead();

        var buf = ArrayPool<byte>.Shared.Rent(65536);

        try
        {
            for (var j = 0; j < patch.Hashes.Length; j++)
            {
                sha1.Initialize();
                var remaining = Math.Min(patch.Length - (j * patch.HashBlockSize), patch.HashBlockSize);

                while (remaining > 0)
                {
                    var r = (int)Math.Min(buf.Length, remaining);
                    if (r != await f.ReadAsync(buf, 0, r, cancellationToken))
                        throw new IOException("Failed to read wholly");

                    sha1.TransformBlock(buf, 0, r, null, 0);
                    remaining -= r;
                }

                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                if (string.Join("", sha1.Hash.Select(x => x.ToString("x2"))) != patch.Hashes[j])
                {
                    return false;
                }
            }

            return true;
        } finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static bool TryReadPatchListEntries(string path, out PatchListEntry[] entries)
    {
        entries = null!;
        if (!File.Exists(path))
            return false;

        try
        {
            if (JsonConvert.DeserializeObject<PatchListEntry[]>(File.ReadAllText(path)) is { } r)
            {
                Log.Information($"Using cached file file: {path}");
                entries = r;
                return true;
            }
        }
        catch (Exception e)
        {
            Log.Information(e, $"Ignoring file: {path}");
        }

        return false;
    }

    private static string EnsureRelativePath(string path)
    {
        while (true)
        {
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
            {
                path = path.Substring(1);
                continue;
            }

            if (path.StartsWith("./", StringComparison.Ordinal) || path.StartsWith(".\\", StringComparison.Ordinal))
            {
                path = path.Substring(2);
                continue;
            }

            if (path.StartsWith("../", StringComparison.Ordinal) || path.StartsWith("..\\", StringComparison.Ordinal))
            {
                path = path.Substring(3);
                continue;
            }

            return path;
        }
    }

    private class TempSettings : ISettings
    {
        public TempSettings(DirectoryInfo patchPath)
        {
            this.PatchPath = patchPath;
        }

        public string AcceptLanguage => "en-US";
        public ClientLanguage? ClientLanguage => Common.ClientLanguage.English;
        public bool? KeepPatches => true;
        public DirectoryInfo PatchPath { get; }
        public DirectoryInfo GamePath => this.PatchPath;
        public AcquisitionMethod? PatchAcquisitionMethod => AcquisitionMethod.NetDownloader;
        public long SpeedLimitBytes { get; set; }
        public int DalamudInjectionDelayMs => 0;
    }
}
