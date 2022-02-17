using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;

namespace XIVLauncher.PatchInstaller.IndexedPatch
{
    public class ZiPatchIndexInstaller : IDisposable
    {
        public readonly ZiPatchIndex Index;
        public readonly List<SortedSet<Tuple<int, int>>> MissingPartIndicesPerPatch = new();
        public readonly List<SortedSet<int>> MissingPartIndicesPerTargetFile = new();
        public readonly SortedSet<int> TooLongTargetFiles = new();

        public int ProgressReportInterval = 250;
        private int LastProgressUpdateReport = 0;
        private readonly List<Stream> TargetStreams = new();

        public delegate void OnCorruptionFoundDelegate(PartialFilePart part, PartialFilePart.VerifyDataResult result);
        public delegate void OnProgressDelegate(PartialFilePart part, long progress, long max);

        public readonly List<OnCorruptionFoundDelegate> OnCorruptionFound = new();
        public readonly List<OnProgressDelegate> OnProgress = new();

        public ZiPatchIndexInstaller(ZiPatchIndex def)
        {
            Index = def;
            foreach (var _ in def.Targets)
            {
                MissingPartIndicesPerTargetFile.Add(new());
                TargetStreams.Add(null);
            }
            foreach (var _ in def.Sources)
                MissingPartIndicesPerPatch.Add(new());
        }

        private void TriggerOnProgress(PartialFilePart part, long progress, long max, bool forceNotify)
        {
            if (!forceNotify)
            {
                if (LastProgressUpdateReport >= 0 && Environment.TickCount < 0)
                {
                    // Overflowed; just report again
                }
                else if (LastProgressUpdateReport + ProgressReportInterval > Environment.TickCount)
                {
                    return;
                }
            }

            LastProgressUpdateReport = Environment.TickCount;
            foreach (var d in OnProgress)
                d(part, progress, max);
        }

        private void TriggerOnCorruptionFound(PartialFilePart part, PartialFilePart.VerifyDataResult result)
        {
            foreach (var d in OnCorruptionFound)
                d(part, result);
        }

        public async Task VerifyFiles(CancellationToken? cancellationToken = null)
        {
            long totalSize = 0;
            foreach (var file in Index.Targets)
                totalSize += file.FileSize;

            List<Task> verifyTasks = new();

            long progressSize = 0;
            for (int targetIndex = 0; targetIndex < Index.Length; targetIndex++)
            {
                if (cancellationToken.HasValue)
                    cancellationToken.Value.ThrowIfCancellationRequested();

                var stream = TargetStreams[targetIndex];
                if (stream == null)
                    continue;

                var file = Index[targetIndex];
                verifyTasks.Add(Task.Run(() =>
                {
                    for (var i = 0; i < file.Count; ++i)
                    {
                        if (cancellationToken.HasValue)
                            cancellationToken.Value.ThrowIfCancellationRequested();

                        var verifyResult = file[i].Verify(stream);
                        lock (verifyTasks)
                        {
                            progressSize += file[i].TargetSize;
                            TriggerOnProgress(file[i], progressSize, totalSize, i == 0);
                            switch (verifyResult)
                            {
                                case PartialFilePart.VerifyDataResult.Pass:
                                    break;

                                case PartialFilePart.VerifyDataResult.FailUnverifiable:
                                    throw new Exception($"{file.RelativePath}:{file[i].TargetOffset}:{file[i].TargetEnd}: Should not happen; unverifiable due to insufficient source data");

                                case PartialFilePart.VerifyDataResult.FailNotEnoughData:
                                case PartialFilePart.VerifyDataResult.FailBadData:
                                    if (file[i].IsFromSourceFile)
                                        MissingPartIndicesPerPatch[file[i].SourceIndex].Add(Tuple.Create(targetIndex, i));
                                    MissingPartIndicesPerTargetFile[targetIndex].Add(i);
                                    TriggerOnCorruptionFound(file[i], verifyResult);
                                    break;
                            }
                        }
                    }
                    if (stream.Length > file.FileSize)
                        TooLongTargetFiles.Add(targetIndex);
                }));
            }
            await Task.WhenAll(verifyTasks);
        }

        public void MarkFileAsMissing(int targetIndex)
        {
            var file = Index[targetIndex];
            for (var i = 0; i < file.Count; ++i)
            {
                if (file[i].IsFromSourceFile)
                    MissingPartIndicesPerPatch[file[i].SourceIndex].Add(Tuple.Create(targetIndex, i));
                MissingPartIndicesPerTargetFile[targetIndex].Add(i);
            }
        }

        public void SetTargetStream(int targetIndex, Stream targetStream)
        {
            TargetStreams[targetIndex] = targetStream;
        }

        public void SetTargetStreamsFromPathReadOnly(string rootPath)
        {
            Dispose();
            for (var i = 0; i < Index.Length; i++)
            {
                var file = Index[i];
                try
                {
                    TargetStreams[i] = new FileStream(Path.Combine(rootPath, file.RelativePath), FileMode.Open, FileAccess.Read);
                }
                catch (FileNotFoundException)
                {
                    MarkFileAsMissing(i);
                }
                catch (DirectoryNotFoundException)
                {
                    MarkFileAsMissing(i);
                }
            }
        }

        public void SetTargetStreamsFromPathWriteMissingOnly(string rootPath)
        {
            Dispose();
            for (var i = 0; i < Index.Length; i++)
            {
                if (MissingPartIndicesPerTargetFile[i].Count == 0 && !TooLongTargetFiles.Contains(i))
                    continue;
                var file = Index[i];
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(rootPath, file.RelativePath)));
                TargetStreams[i] = new FileStream(Path.Combine(rootPath, file.RelativePath), FileMode.OpenOrCreate, FileAccess.ReadWrite);
            }
        }

        public void RepairNonPatchData()
        {
            for (int i = 0, i_ = Index.Length; i < i_; i++)
            {
                var target = TargetStreams[i];
                if (target == null)
                    continue;

                var file = Index[i];
                lock (target)
                {
                    foreach (var partIndex in MissingPartIndicesPerTargetFile[i])
                    {
                        var part = file[partIndex];
                        if (part.IsFromSourceFile)
                            continue;

                        part.Repair(target, (Stream)null);
                    }
                    target.SetLength(file.FileSize);
                }
            }
        }

        public void WriteVersionFiles(string localRootPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(localRootPath, Index.VersionFileVer)));
            using (var writer = new StreamWriter(new FileStream(Path.Combine(localRootPath, Index.VersionFileVer), FileMode.Create, FileAccess.Write)))
                writer.Write(Index.VersionName);
            using (var writer = new StreamWriter(new FileStream(Path.Combine(localRootPath, Index.VersionFileBck), FileMode.Create, FileAccess.Write)))
                writer.Write(Index.VersionName);
        }

        public abstract class InstallTaskConfig : IDisposable
        {
            public long ProgressMax { get; protected set; }
            public long ProgressValue { get; protected set; }
            public readonly ZiPatchIndex Index;
            public readonly ZiPatchIndexInstaller Installer;
            public readonly int SourceIndex;

            public InstallTaskConfig(ZiPatchIndexInstaller installer, int sourceIndex)
            {
                Index = installer.Index;
                Installer = installer;
                SourceIndex = sourceIndex;
            }

            public abstract Task Repair(CancellationToken? cancellationToken = null);

            public abstract PartialFilePart? FirstPart { get; }

            public virtual void Dispose() { }
        }

        public class HttpInstallTaskConfig : InstallTaskConfig
        {
            public readonly HttpClient Client;
            public readonly string SourceUrl;
            public readonly List<Tuple<int, int>> TargetPartIndices;
            private readonly List<long> TargetPartOffsets;

            public HttpInstallTaskConfig(ZiPatchIndexInstaller installer, int sourceIndex, HttpClient client, string sourceUrl, IEnumerable<Tuple<int, int>> targetPartIndices)
                : base(installer, sourceIndex)
            {
                Client = client;
                SourceUrl = sourceUrl;
                TargetPartIndices = targetPartIndices.ToList();
                TargetPartIndices.Sort((x, y) => Index[x.Item1][x.Item2].SourceOffset.CompareTo(Index[y.Item1][y.Item2].SourceOffset));
                TargetPartOffsets = TargetPartIndices.Select(x => Index[x.Item1][x.Item2].SourceOffset).ToList();

                long totalTargetSize = 0;
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    totalTargetSize += Index[targetIndex][partIndex].TargetSize;
                ProgressMax = totalTargetSize;
            }

            public override PartialFilePart? FirstPart => TargetPartIndices.Any() ? Index[TargetPartIndices.First().Item1][TargetPartIndices.First().Item2] : null;

            public override async Task Repair(CancellationToken? cancellationToken = null)
            {
                var offsets = new List<Tuple<long, long>>();
                offsets.Clear();
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    offsets.Add(Tuple.Create(Index[targetIndex][partIndex].SourceOffset, Math.Min(Index.GetSourceLastPtr(SourceIndex), Index[targetIndex][partIndex].MaxSourceEnd)));
                offsets.Sort();

                for (int j = 1; j < offsets.Count;)
                {
                    if (offsets[j].Item1 - offsets[j - 1].Item2 < 128)
                    {
                        offsets[j - 1] = Tuple.Create(offsets[j - 1].Item1, offsets[j].Item2);
                        offsets.RemoveAt(j);
                    }
                    else
                        j += 1;
                }

                using HttpRequestMessage req = new(HttpMethod.Get, SourceUrl);
                req.Headers.Add("Range", "bytes=" + string.Join(",", offsets.Select(x => $"{x.Item1}-{x.Item2 - 1}")));
                // req.Headers.Add("X-Patch-Unique-Id")
                req.Headers.Add("User-Agent", "FFXIV PATCH CLIENT");
                req.Headers.Add("Connection", "Keep-Alive");
                using var resp = new MultipartRequestHandler(await Client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead));

                List<Stream> ss = new();
                while (true)
                {
                    if (cancellationToken.HasValue)
                        cancellationToken.Value.ThrowIfCancellationRequested();

                    using var n = await resp.NextPart(cancellationToken);
                    ss.Add(n);
                    if (n == null)
                        break;

                    var fromTupleIndex = TargetPartOffsets.BinarySearch(n.AvailableFromOffset);
                    if (fromTupleIndex < 0)
                        fromTupleIndex = ~fromTupleIndex;
                    while (fromTupleIndex > 0 && TargetPartOffsets[fromTupleIndex - 1] >= n.AvailableFromOffset)
                        fromTupleIndex--;

                    for (var i = fromTupleIndex; i < TargetPartOffsets.Count && TargetPartOffsets[i] < n.AvailableToOffset; i++)
                    {
                        if (cancellationToken.HasValue)
                            cancellationToken.Value.ThrowIfCancellationRequested();

                        var (targetIndex, partIndex) = TargetPartIndices[i];
                        var part = Index[targetIndex][partIndex];
                        ProgressValue += part.TargetSize;

                        var target = Installer.TargetStreams[part.TargetIndex];
                        if (target != null)
                        {
                            lock (target)
                            {
                                try
                                {
                                    part.Repair(target, n);
                                }
                                catch (PartialFilePart.InsufficientReconstructionDataException)
                                {
                                    break;
                                }
                            }
                        }

                        TargetPartIndices.RemoveAt(i);
                        TargetPartOffsets.RemoveAt(i);
                        i--;
                    }
                }
                if (TargetPartIndices.Any())
                    throw new IOException("Missing target part remains");
            }
        }

        public class StreamInstallTaskConfig : InstallTaskConfig
        {
            public readonly Stream SourceStream;
            public readonly IList<Tuple<long, long>> SourceOffsets;

            public StreamInstallTaskConfig(ZiPatchIndexInstaller installer, int sourceIndex, Stream sourceStream)
                : base(installer, sourceIndex)
            {
                SourceStream = sourceStream;
                long totalTargetSize = 0;
                foreach (var (targetIndex, partIndex) in Installer.MissingPartIndicesPerPatch[sourceIndex])
                    totalTargetSize += Index[targetIndex][partIndex].TargetSize;
                ProgressMax = totalTargetSize;
            }

            public override PartialFilePart? FirstPart => Installer.MissingPartIndicesPerPatch[SourceIndex].Any() ? Index[Installer.MissingPartIndicesPerPatch[SourceIndex].First().Item1][Installer.MissingPartIndicesPerPatch[SourceIndex].First().Item2] : null;

            public override async Task Repair(CancellationToken? cancellationToken)
            {
                await Task.Run(() =>
                {
                    foreach (var (targetIndex, partIndex) in Installer.MissingPartIndicesPerPatch[SourceIndex])
                    {
                        if (cancellationToken.HasValue)
                            cancellationToken.Value.ThrowIfCancellationRequested();
                        var part = Index[targetIndex][partIndex];
                        ProgressValue += part.TargetSize;

                        var target = Installer.TargetStreams[part.TargetIndex];
                        if (target == null)
                            continue;
                        lock (target)
                        {
                            part.Repair(target, SourceStream);
                        }
                    }
                });
            }

            public override void Dispose()
            {
                SourceStream.Dispose();
                base.Dispose();
            }
        }

        private readonly List<InstallTaskConfig> InstallTaskConfigs = new();

        public void QueueInstall(int sourceIndex, HttpClient client, string sourceUrl, ISet<Tuple<int, int>> targetPartIndices)
        {
            if (targetPartIndices.Any())
                InstallTaskConfigs.Add(new HttpInstallTaskConfig(this, sourceIndex, client, sourceUrl, targetPartIndices));
        }

        public void QueueInstall(int sourceIndex, HttpClient client, string sourceUrl, int splitBy = 8, int maxPartsPerRequest = 1024)
        {
            var indices = MissingPartIndicesPerPatch[sourceIndex];
            var indicesPerRequest = Math.Min((int)Math.Ceiling(1.0 * indices.Count / splitBy), maxPartsPerRequest);
            for (int j = 0; j < indices.Count; j += indicesPerRequest)
                QueueInstall(sourceIndex, client, sourceUrl, indices.Skip(j).Take(Math.Min(indicesPerRequest, indices.Count - j)).ToHashSet());
        }

        public void QueueInstall(int sourceIndex, Stream stream)
        {
            InstallTaskConfigs.Add(new StreamInstallTaskConfig(this, sourceIndex, stream));
        }

        public async Task Install(int concurrentCount, CancellationToken? cancellationToken = null)
        {
            long progressMax = 0;
            foreach (var config in InstallTaskConfigs)
                progressMax += config.ProgressMax;

            Task progressReportTask = null;
            Queue<InstallTaskConfig> pendingTaskConfigs = new();
            foreach (var x in InstallTaskConfigs)
                pendingTaskConfigs.Enqueue(x);

            Dictionary<Task, InstallTaskConfig> runningTasks = new();
            PartialFilePart? lastBegunTaskFirstPart = null;
            while (pendingTaskConfigs.Any() || runningTasks.Any())
            {
                if (cancellationToken.HasValue)
                    cancellationToken.Value.ThrowIfCancellationRequested();

                while (pendingTaskConfigs.Any() && runningTasks.Count < concurrentCount)
                {
                    var config = pendingTaskConfigs.Dequeue();
                    if (config.FirstPart.HasValue)
                    {
                        lastBegunTaskFirstPart = config.FirstPart.Value;
                        runningTasks[config.Repair(cancellationToken)] = config;
                    }
                }

                long progressSum = 0;
                foreach (var config2 in InstallTaskConfigs)
                    progressSum += config2.ProgressValue;
                if (lastBegunTaskFirstPart.HasValue)
                    TriggerOnProgress(lastBegunTaskFirstPart.Value, progressSum, progressMax, true);

                if (progressReportTask == null || progressReportTask.IsCompleted)
                    progressReportTask = cancellationToken.HasValue ? Task.Delay(ProgressReportInterval, cancellationToken.Value) : Task.Delay(250);
                runningTasks[progressReportTask] = null;
                await Task.WhenAny(runningTasks.Keys);
                foreach (var kvp in runningTasks)
                {
                    if (!kvp.Key.IsFaulted || kvp.Value == null)
                        continue;

                    if (kvp.Key.Exception.InnerException is IOException)
                    {
                        Log.Warning(kvp.Key.Exception, "IOException occurred while loading part; trying again");
                        pendingTaskConfigs.Enqueue(kvp.Value);
                    }
                    else
                        throw kvp.Key.Exception;
                }
                runningTasks.Keys.Where(p => p.IsCompleted || p.IsCanceled || p.IsFaulted || p == progressReportTask).ToList().ForEach(p => runningTasks.Remove(p));
            }
            RepairNonPatchData();
        }

        public void Dispose()
        {
            for (var i = 0; i < TargetStreams.Count; i++)
            {
                if (TargetStreams[i] != null)
                {
                    TargetStreams[i].Dispose();
                    TargetStreams[i] = null;
                }
            }
            foreach (var item in InstallTaskConfigs)
                item.Dispose();
            InstallTaskConfigs.Clear();
        }
    }
}
