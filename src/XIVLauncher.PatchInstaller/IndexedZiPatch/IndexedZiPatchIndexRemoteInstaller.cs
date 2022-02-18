using Serilog;
using SharedMemory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.IndexedZiPatch
{
    public class IndexedZiPatchIndexRemoteInstaller : IDisposable
    {
        private readonly Process WorkerProcess;
        private readonly RpcBuffer SubprocessBuffer;
        private int CancellationTokenCounter = 1;
        private long LastProgressUpdateCounter = 0;
        private bool IsDisposed = false;

        public readonly List<IndexedZiPatchInstaller.OnProgressDelegate> OnProgress = new();

        public IndexedZiPatchIndexRemoteInstaller(string workerExecutablePath, bool asAdmin)
        {
            var rpcChannelName = "RemoteZiPatchIndexInstaller" + Guid.NewGuid().ToString();
            SubprocessBuffer = new RpcBuffer(rpcChannelName, RpcResponseHandler);

            if (workerExecutablePath != null)
            {
                WorkerProcess = new();
                WorkerProcess.StartInfo.FileName = workerExecutablePath;
                WorkerProcess.StartInfo.UseShellExecute = true;
                WorkerProcess.StartInfo.Verb = asAdmin ? "runas" : "open";
                WorkerProcess.StartInfo.Arguments = $"index-rpc {Process.GetCurrentProcess().Id} {rpcChannelName}";
                WorkerProcess.Start();
            }
            else
            {
                WorkerProcess = null;
                Task.Run(() => new WorkerSubprocessBody(Process.GetCurrentProcess().Id, rpcChannelName).RunToDisposeSelf());
            }
        }

        public void Dispose()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            WaitForResult(GetRequestCreator(WorkerInboundOpcode.Dispose, null)).Wait();

            if (WorkerProcess != null)
            {
                if (!WorkerProcess.HasExited)
                {
                    WorkerProcess.Kill();
                    WorkerProcess.WaitForExit();
                }
            }
            SubprocessBuffer.Dispose();
            IsDisposed = true;
        }

        private void RpcResponseHandler(ulong _, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            var type = (WorkerOutboundOpcode)reader.ReadInt32();
            switch (type)
            {
                case WorkerOutboundOpcode.UpdateProgress:
                    OnReceiveProgressUpdate(reader);
                    break;

                default:
                    throw new ArgumentException("Unknown recv opc");
            }
        }

        private void OnReceiveProgressUpdate(BinaryReader reader)
        {
            var progressUpdateCounter = reader.ReadInt64();
            if (progressUpdateCounter < LastProgressUpdateCounter)
                return;

            LastProgressUpdateCounter = progressUpdateCounter;
            var index = reader.ReadInt32();
            var progress = reader.ReadInt64();
            var max = reader.ReadInt64();
            foreach (var d in OnProgress)
                d(index, progress, max);
        }

        private BinaryWriter GetRequestCreator(WorkerInboundOpcode opcode, CancellationToken? cancellationToken)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            var tokenId = -1;
            if (cancellationToken.HasValue)
            {
                tokenId = CancellationTokenCounter++;
                cancellationToken.Value.Register(async () => await CancelRemoteTask(tokenId));
            }
            writer.Write(tokenId);
            writer.Write((int)opcode);
            return writer;
        }

        private async Task<BinaryReader> WaitForResult(BinaryWriter req, int timeoutMs = 30000, bool autoDispose = true)
        {
            var reader = new BinaryReader(new MemoryStream((await SubprocessBuffer.RemoteRequestAsync(((MemoryStream)req.BaseStream).ToArray(), timeoutMs)).Data));
            try
            {
                var result = (WorkerResultCode)reader.ReadInt32();
                return result switch
                {
                    WorkerResultCode.Pass => reader,
                    WorkerResultCode.Cancelled => throw new TaskCanceledException(),
                    WorkerResultCode.Error => throw new Exception(reader.ReadString()),
                    _ => throw new InvalidOperationException("Invalid WorkerResultCodes"),
                };
            }
            finally
            {
                if (autoDispose)
                    reader.Dispose();
            }
        }

        private async Task CancelRemoteTask(int tokenId)
        {
            if (IsDisposed)
                return;

            var writer = GetRequestCreator(WorkerInboundOpcode.CancelTask, null);
            writer.Write(tokenId);
            await WaitForResult(writer);
        }

        public async Task ConstructFromPatchFile(IndexedZiPatchIndex patchIndex)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.Construct, null);
            patchIndex.WriteTo(writer);
            await WaitForResult(writer);
        }

        public async Task VerifyFiles(int concurrentCount = 8, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.VerifyFiles, cancellationToken);
            writer.Write(concurrentCount);
            await WaitForResult(writer);
        }

        public async Task MarkFileAsMissing(int targetIndex, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.MarkFileAsMissing, cancellationToken);
            writer.Write(targetIndex);
            await WaitForResult(writer);
        }

        public async Task SetTargetStreamFromPathReadOnly(int targetIndex, string path, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamFromPathReadOnly, cancellationToken);
            writer.Write(targetIndex);
            writer.Write(path);
            await WaitForResult(writer);
        }

        public async Task SetTargetStreamFromPathReadWrite(int targetIndex, string path, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamFromPathReadWrite, cancellationToken);
            writer.Write(targetIndex);
            writer.Write(path);
            await WaitForResult(writer);
        }

        public async Task SetTargetStreamsFromPathReadOnly(string rootPath, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamsFromPathReadOnly, cancellationToken);
            writer.Write(rootPath);
            await WaitForResult(writer);
        }

        public async Task SetTargetStreamsFromPathReadWriteForMissingFiles(string rootPath, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamsFromPathReadWriteForMissingFiles, cancellationToken);
            writer.Write(rootPath);
            await WaitForResult(writer);
        }

        public async Task RepairNonPatchData(CancellationToken? cancellationToken = null) => await WaitForResult(GetRequestCreator(WorkerInboundOpcode.RepairNonPatchData, cancellationToken));

        public async Task WriteVersionFiles(string rootPath, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.WriteVersionFiles, cancellationToken);
            writer.Write(rootPath);
            await WaitForResult(writer);
        }

        public async Task QueueInstall(int sourceIndex, string sourceUrl, string sid, int splitBy = 8, int maxPartsPerRequest = 1024, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.QueueInstall, cancellationToken);
            writer.Write(sourceIndex);
            writer.Write(sourceUrl);
            writer.Write(sid ?? "");
            writer.Write(splitBy);
            writer.Write(maxPartsPerRequest);
            await WaitForResult(writer);
        }

        public async Task Install(int concurrentCount, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.Install, cancellationToken);
            writer.Write(concurrentCount);
            await WaitForResult(writer, 864000000);
        }

        public async Task<List<SortedSet<Tuple<int, int>>>> GetMissingPartIndicesPerPatch()
        {
            using var reader = await WaitForResult(GetRequestCreator(WorkerInboundOpcode.GetMissingPartIndicesPerPatch, null), 30000, false);
            List<SortedSet<Tuple<int, int>>> result = new();
            for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
            {
                SortedSet<Tuple<int, int>> e1 = new();
                for (int j = 0, j_ = reader.ReadInt32(); j < j_; j++)
                    e1.Add(Tuple.Create(reader.ReadInt32(), reader.ReadInt32()));
                result.Add(e1);
            }
            return result;
        }

        public async Task<List<SortedSet<int>>> GetMissingPartIndicesPerTargetFile()
        {
            using var reader = await WaitForResult(GetRequestCreator(WorkerInboundOpcode.GetMissingPartIndicesPerTargetFile, null), 30000, false);
            List<SortedSet<int>> result = new();
            for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
            {
                SortedSet<int> e1 = new();
                for (int j = 0, j_ = reader.ReadInt32(); j < j_; j++)
                    e1.Add(reader.ReadInt32());
                result.Add(e1);
            }
            return result;
        }

        public async Task<SortedSet<int>> GetTooLongTargetFiles()
        {
            using var reader = await WaitForResult(GetRequestCreator(WorkerInboundOpcode.GetTooLongTargetFiles, null), 30000, false);
            SortedSet<int> result = new();
            for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
                result.Add(reader.ReadInt32());
            return result;
        }

        public class WorkerSubprocessBody : IDisposable
        {
            private readonly Process ParentProcess;
            private readonly RpcBuffer SubprocessBuffer;
            private readonly HttpClient Client = new();
            private readonly Dictionary<int, CancellationTokenSource> CancellationTokenSources = new();
            private IndexedZiPatchInstaller Instance = null;
            private long ProgressUpdateCounter = 0;

            public WorkerSubprocessBody(int monitorProcessId, string channelName)
            {
                ParentProcess = Process.GetProcessById(monitorProcessId);
                SubprocessBuffer = new RpcBuffer(channelName, async (ulong _, byte[] data) =>
                {
                    using var reader = new BinaryReader(new MemoryStream(data));
                    var cancelSourceId = reader.ReadInt32();
                    CancellationToken? cancelToken = null;
                    if (cancelSourceId != -1)
                    {
                        CancellationTokenSources[cancelSourceId] = new CancellationTokenSource();
                        cancelToken = CancellationTokenSources[cancelSourceId].Token;
                    }
                    var method = (WorkerInboundOpcode)reader.ReadInt32();

                    var ms = new MemoryStream();
                    var writer = new BinaryWriter(ms);
                    writer.Write(0);

                    try
                    {
                        switch (method)
                        {
                            case WorkerInboundOpcode.CancelTask:
                                lock (CancellationTokenSources)
                                {
                                    if (CancellationTokenSources.TryGetValue(reader.ReadInt32(), out var cts))
                                        cts.Cancel();
                                }
                                break;

                            case WorkerInboundOpcode.Construct:
                                Instance?.Dispose();
                                Instance = new(new IndexedZiPatchIndex(reader, false));
                                Instance.OnProgress.Add(OnProgressUpdate);
                                break;

                            case WorkerInboundOpcode.Dispose:
                                Instance?.Dispose();
                                Instance = null;
                                break;

                            case WorkerInboundOpcode.VerifyFiles:
                                await Instance.VerifyFiles(reader.ReadInt32(), cancelToken);
                                break;

                            case WorkerInboundOpcode.MarkFileAsMissing:
                                Instance.MarkFileAsMissing(reader.ReadInt32());
                                break;

                            case WorkerInboundOpcode.SetTargetStreamFromPathReadOnly:
                                Instance.SetTargetStream(reader.ReadInt32(), new FileStream(reader.ReadString(), FileMode.Open, FileAccess.Read));
                                break;

                            case WorkerInboundOpcode.SetTargetStreamFromPathReadWrite:
                                Instance.SetTargetStream(reader.ReadInt32(), new FileStream(reader.ReadString(), FileMode.OpenOrCreate, FileAccess.ReadWrite));
                                break;

                            case WorkerInboundOpcode.SetTargetStreamsFromPathReadOnly:
                                Instance.SetTargetStreamsFromPathReadOnly(reader.ReadString());
                                break;

                            case WorkerInboundOpcode.SetTargetStreamsFromPathReadWriteForMissingFiles:
                                Instance.SetTargetStreamsFromPathReadWriteForMissingFiles(reader.ReadString());
                                break;

                            case WorkerInboundOpcode.RepairNonPatchData:
                                await Instance.RepairNonPatchData(cancelToken);
                                break;

                            case WorkerInboundOpcode.WriteVersionFiles:
                                Instance.WriteVersionFiles(reader.ReadString());
                                break;

                            case WorkerInboundOpcode.QueueInstall:
                                Instance.QueueInstall(reader.ReadInt32(), Client, reader.ReadString(), reader.ReadString(), reader.ReadInt32(), reader.ReadInt32());
                                break;

                            case WorkerInboundOpcode.Install:
                                await Instance.Install(reader.ReadInt32(), cancelToken);
                                break;

                            case WorkerInboundOpcode.GetMissingPartIndicesPerPatch:
                                writer.Write(Instance.MissingPartIndicesPerPatch.Count);
                                foreach (var e1 in Instance.MissingPartIndicesPerPatch)
                                {
                                    writer.Write(e1.Count);
                                    foreach (var e2 in e1)
                                    {
                                        writer.Write(e2.Item1);
                                        writer.Write(e2.Item2);
                                    }
                                }
                                break;

                            case WorkerInboundOpcode.GetMissingPartIndicesPerTargetFile:
                                writer.Write(Instance.MissingPartIndicesPerTargetFile.Count);
                                foreach (var e1 in Instance.MissingPartIndicesPerTargetFile)
                                {
                                    writer.Write(e1.Count);
                                    foreach (var e2 in e1)
                                        writer.Write(e2);
                                }
                                break;

                            case WorkerInboundOpcode.GetTooLongTargetFiles:
                                writer.Write(Instance.TooLongTargetFiles.Count);
                                foreach (var e1 in Instance.TooLongTargetFiles)
                                    writer.Write(e1);
                                break;

                            default:
                                throw new InvalidOperationException("Invalid WorkerInboundOpcode");
                        }

                        writer.Seek(0, SeekOrigin.Begin);
                        writer.Write((int)WorkerResultCode.Pass);
                    }
                    catch (Exception ex)
                    {
                        writer.Seek(0, SeekOrigin.Begin);
                        if (ex is OperationCanceledException)
                            writer.Write((int)WorkerResultCode.Cancelled);
                        else
                        {
                            writer.Write((int)WorkerResultCode.Error);
                            writer.Write(ex.ToString());
                        }
                    }
                    finally
                    {
                        if (cancelSourceId != -1)
                            CancellationTokenSources.Remove(cancelSourceId);
                    }
                    return ms.ToArray();
                });
            }

            private void OnProgressUpdate(int index, long progress, long max)
            {
                lock (this)
                {
                    var ms = new MemoryStream();
                    var writer = new BinaryWriter(ms);
                    writer.Write((int)WorkerOutboundOpcode.UpdateProgress);
                    writer.Write(ProgressUpdateCounter);
                    writer.Write(index);
                    writer.Write(progress);
                    writer.Write(max);
                    ProgressUpdateCounter += 1;
                    SubprocessBuffer.RemoteRequestAsync(ms.ToArray());
                }
            }

            public void Dispose()
            {
                SubprocessBuffer.Dispose();
                Client.Dispose();
                Instance?.Dispose();
            }

            public void Run()
            {
                ParentProcess.WaitForExit();
            }

            public void RunToDisposeSelf()
            {
                try
                {
                    Run();
                }
                catch (OperationCanceledException)
                {
                    // pass
                }
                finally
                {
                    Dispose();
                }
            }
        }

        private enum WorkerResultCode : int
        {
            Pass,
            Cancelled,
            Error,
        }

        private enum WorkerOutboundOpcode : int
        {
            UpdateProgress,
        }

        private enum WorkerInboundOpcode : int
        {
            CancelTask,
            Construct,
            Dispose,
            VerifyFiles,
            MarkFileAsMissing,
            SetTargetStreamFromPathReadOnly,
            SetTargetStreamFromPathReadWrite,
            SetTargetStreamsFromPathReadOnly,
            SetTargetStreamsFromPathReadWriteForMissingFiles,
            RepairNonPatchData,
            WriteVersionFiles,
            QueueInstall,
            Install,
            GetMissingPartIndicesPerPatch,
            GetMissingPartIndicesPerTargetFile,
            GetTooLongTargetFiles,
        }

        public static void Test()
        {
            Task.Run(async () =>
            {
                // Cancel in 15 secs
                var cancellationTokenSource = new CancellationTokenSource(15000);
                var cancellationToken = cancellationTokenSource.Token;

                var availableSourceUrls = new Dictionary<string, string>() {
                    {"boot:D2013.06.18.0000.0000.patch", "http://patch-dl.ffxiv.com/boot/2b5cbc63/D2013.06.18.0000.0000.patch"},
                    {"boot:D2021.11.16.0000.0001.patch", "http://patch-dl.ffxiv.com/boot/2b5cbc63/D2021.11.16.0000.0001.patch"},
                };
                var maxConcurrentConnectionsForPatchSet = 8;

                var rootAndPatchPairs = new List<Tuple<string, string>>() {
                    Tuple.Create(@"Z:\tgame\boot", @"Z:\patch-dl.ffxiv.com\boot\2b5cbc63\D2021.11.16.0000.0001.patch.index"),
                };

                // Run verifier as subprocess
                using var verifier = new IndexedZiPatchIndexRemoteInstaller(System.Reflection.Assembly.GetExecutingAssembly().Location, true);
                // Run verifier as another thread
                // using var verifier = new IndexedZiPatchIndexRemoteInstaller(null, true);

                foreach (var (gameRootPath, patchIndexFilePath) in rootAndPatchPairs)
                {
                    var patchIndex = new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));

                    await verifier.ConstructFromPatchFile(patchIndex);

                    verifier.OnProgress.Add((int index, long progress, long max) => Log.Information("[{0}/{1}] Checking file {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", index + 1, patchIndex.Length, patchIndex[Math.Min(index, patchIndex.Length - 1)].RelativePath, progress / 1048576.0, max / 1048576.0, 100.0 * progress / max));
                    await verifier.SetTargetStreamsFromPathReadOnly(gameRootPath);
                    // TODO: check one at a time if random access is slow?
                    await verifier.VerifyFiles(Environment.ProcessorCount, cancellationToken);
                    verifier.OnProgress.Clear();

                    var missing = await verifier.GetMissingPartIndicesPerPatch();

                    verifier.OnProgress.Add((int index, long progress, long max) => Log.Information("[{0}/{1}] Installing {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", index + 1, patchIndex.Sources.Count, patchIndex.Sources[Math.Min(index, patchIndex.Sources.Count - 1)], progress / 1048576.0, max / 1048576.0, 100.0 * progress / max));
                    await verifier.SetTargetStreamsFromPathReadWriteForMissingFiles(gameRootPath);
                    var prefix = patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? "boot:" : $"ex{patchIndex.ExpacVersion}:";
                    for (var i = 0; i < patchIndex.Sources.Count; i++)
                    {
                        if (!missing[i].Any())
                            continue;

                        await verifier.QueueInstall(i, availableSourceUrls[prefix + patchIndex.Sources[i]], null, maxConcurrentConnectionsForPatchSet);
                    }
                    await verifier.Install(maxConcurrentConnectionsForPatchSet, cancellationToken);
                    await verifier.WriteVersionFiles(gameRootPath);
                    verifier.OnProgress.Clear();
                }
            }).Wait();
        }
    }
}
