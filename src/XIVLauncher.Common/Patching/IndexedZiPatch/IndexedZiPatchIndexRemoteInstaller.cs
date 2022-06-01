using Serilog;
using SharedMemory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    public class IndexedZiPatchIndexRemoteInstaller : IDisposable
    {
        private readonly Process workerProcess;
        private readonly RpcBuffer subprocessBuffer;
        private int cancellationTokenCounter = 1;
        private long lastProgressUpdateCounter = 0;
        private bool isDisposed = false;

        public event IndexedZiPatchInstaller.OnInstallProgressDelegate OnInstallProgress;
        public event IndexedZiPatchInstaller.OnVerifyProgressDelegate OnVerifyProgress;

        public IndexedZiPatchIndexRemoteInstaller(string workerExecutablePath, bool asAdmin)
        {
            var rpcChannelName = "RemoteZiPatchIndexInstaller" + Guid.NewGuid().ToString();
            this.subprocessBuffer = new RpcBuffer(rpcChannelName, RpcResponseHandler);

            if (workerExecutablePath != null)
            {
                this.workerProcess = new();
                this.workerProcess.StartInfo.FileName = workerExecutablePath;
                this.workerProcess.StartInfo.UseShellExecute = true;
                this.workerProcess.StartInfo.Verb = asAdmin ? "runas" : "open";
                this.workerProcess.StartInfo.Arguments = $"index-rpc {Process.GetCurrentProcess().Id} {rpcChannelName}";
                this.workerProcess.Start();
            }
            else
            {
                this.workerProcess = null;
                Task.Run(() => new WorkerSubprocessBody(Process.GetCurrentProcess().Id, rpcChannelName).RunToDisposeSelf());
            }
        }

        public void Dispose()
        {
            if (this.isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            try
            {
                this.subprocessBuffer.RemoteRequest(((MemoryStream)GetRequestCreator(WorkerInboundOpcode.DisposeAndExit, null).BaseStream).ToArray(), 100);
            }
            catch (Exception)
            {
                // ignore any exception
            }

            if (this.workerProcess != null && !this.workerProcess.HasExited)
            {
                this.workerProcess.WaitForExit(1000);
                try
                {
                    this.workerProcess.Kill();
                }
                catch (Exception)
                {
                    if (!this.workerProcess.HasExited)
                        throw;
                }
            }
            this.subprocessBuffer.Dispose();
            this.isDisposed = true;
        }

        private void RpcResponseHandler(ulong _, byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            var type = (WorkerOutboundOpcode)reader.ReadInt32();
            switch (type)
            {
                case WorkerOutboundOpcode.UpdateInstallProgress:
                    OnReceiveInstallProgressUpdate(reader);
                    break;

                case WorkerOutboundOpcode.UpdateVerifyProgress:
                    OnReceiveVerifyProgressUpdate(reader);
                    break;

                default:
                    throw new ArgumentException("Unknown recv opc");
            }
        }

        private void OnReceiveInstallProgressUpdate(BinaryReader reader)
        {
            var progressUpdateCounter = reader.ReadInt64();
            if (progressUpdateCounter < this.lastProgressUpdateCounter)
                return;

            this.lastProgressUpdateCounter = progressUpdateCounter;
            var index = reader.ReadInt32();
            var progress = reader.ReadInt64();
            var max = reader.ReadInt64();
            var state = (IndexedZiPatchInstaller.InstallTaskState)reader.ReadInt32();

            OnInstallProgress?.Invoke(index, progress, max, state);
        }

        private void OnReceiveVerifyProgressUpdate(BinaryReader reader)
        {
            var progressUpdateCounter = reader.ReadInt64();
            if (progressUpdateCounter < this.lastProgressUpdateCounter)
                return;

            this.lastProgressUpdateCounter = progressUpdateCounter;
            var index = reader.ReadInt32();
            var progress = reader.ReadInt64();
            var max = reader.ReadInt64();

            OnVerifyProgress?.Invoke(index, progress, max);
        }

        private BinaryWriter GetRequestCreator(WorkerInboundOpcode opcode, CancellationToken? cancellationToken)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            var tokenId = -1;
            if (cancellationToken.HasValue)
            {
                tokenId = this.cancellationTokenCounter++;
                cancellationToken.Value.Register(async () => await CancelRemoteTask(tokenId));
            }
            writer.Write(tokenId);
            writer.Write((int)opcode);
            return writer;
        }

        private async Task<BinaryReader> WaitForResult(BinaryWriter req, CancellationToken? cancellationToken, int timeoutMs = 30000, bool autoDispose = true)
        {
            var requestData = ((MemoryStream)req.BaseStream).ToArray();
            RpcResponse response;
            if (cancellationToken.HasValue)
                response = await this.subprocessBuffer.RemoteRequestAsync(requestData, timeoutMs, cancellationToken.Value);
            else
                response = await this.subprocessBuffer.RemoteRequestAsync(requestData, timeoutMs);
            if (cancellationToken.HasValue)
                cancellationToken.Value.ThrowIfCancellationRequested();

            if (this.isDisposed)
                throw new OperationCanceledException();
            var reader = new BinaryReader(new MemoryStream(response.Data));
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
            if (this.isDisposed)
                return;

            try
            {
                var writer = GetRequestCreator(WorkerInboundOpcode.CancelTask, null);
                writer.Write(tokenId);
                await WaitForResult(writer, null);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        public async Task ConstructFromPatchFile(IndexedZiPatchIndex patchIndex, int progressReportInterval = 250)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.Construct, null);
            patchIndex.WriteTo(writer);
            writer.Write(progressReportInterval);
            await WaitForResult(writer, null);
        }

        public async Task VerifyFiles(bool refine = false, int concurrentCount = 8, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.VerifyFiles, cancellationToken);
            writer.Write(refine);
            writer.Write(concurrentCount);
            await WaitForResult(writer, cancellationToken, 864000000);
        }

        public async Task MarkFileAsMissing(int targetIndex, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.MarkFileAsMissing, cancellationToken);
            writer.Write(targetIndex);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task SetTargetStreamFromPathReadOnly(int targetIndex, string path, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamFromPathReadOnly, cancellationToken);
            writer.Write(targetIndex);
            writer.Write(path);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task SetTargetStreamFromPathReadWrite(int targetIndex, string path, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamFromPathReadWrite, cancellationToken);
            writer.Write(targetIndex);
            writer.Write(path);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task SetTargetStreamsFromPathReadOnly(string rootPath, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamsFromPathReadOnly, cancellationToken);
            writer.Write(rootPath);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task SetTargetStreamsFromPathReadWriteForMissingFiles(string rootPath, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetTargetStreamsFromPathReadWriteForMissingFiles, cancellationToken);
            writer.Write(rootPath);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task RepairNonPatchData(CancellationToken? cancellationToken = null) => await WaitForResult(GetRequestCreator(WorkerInboundOpcode.RepairNonPatchData, cancellationToken), cancellationToken);

        public async Task WriteVersionFiles(string rootPath, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.WriteVersionFiles, cancellationToken);
            writer.Write(rootPath);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task QueueInstall(int sourceIndex, Uri sourceUrl, string sid, int splitBy = 8, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.QueueInstallFromUrl, cancellationToken);
            writer.Write(sourceIndex);
            writer.Write(sourceUrl.OriginalString);
            writer.Write(sid ?? "");
            writer.Write(splitBy);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task QueueInstall(int sourceIndex, FileInfo sourceFile, int splitBy = 8, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.QueueInstallFromLocalFile, cancellationToken);
            writer.Write(sourceIndex);
            writer.Write(sourceFile.FullName);
            writer.Write(splitBy);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task Install(int concurrentCount, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.Install, cancellationToken);
            writer.Write(concurrentCount);
            await WaitForResult(writer, cancellationToken, 864000000);
        }

        public async Task<List<SortedSet<Tuple<int, int>>>> GetMissingPartIndicesPerPatch(CancellationToken? cancellationToken = null)
        {
            using var reader = await WaitForResult(GetRequestCreator(WorkerInboundOpcode.GetMissingPartIndicesPerPatch, cancellationToken), cancellationToken, 30000, false);
            List<SortedSet<Tuple<int, int>>> result = new();
            for (int i = 0, iReadLength = reader.ReadInt32(); i < iReadLength; i++)
            {
                SortedSet<Tuple<int, int>> e1 = new();
                for (int j = 0, jReadLength = reader.ReadInt32(); j < jReadLength; j++)
                    e1.Add(Tuple.Create(reader.ReadInt32(), reader.ReadInt32()));
                result.Add(e1);
            }
            return result;
        }

        public async Task<List<SortedSet<int>>> GetMissingPartIndicesPerTargetFile(CancellationToken? cancellationToken = null)
        {
            using var reader = await WaitForResult(GetRequestCreator(WorkerInboundOpcode.GetMissingPartIndicesPerTargetFile, cancellationToken), cancellationToken, 30000, false);
            List<SortedSet<int>> result = new();
            for (int i = 0, iReadLength = reader.ReadInt32(); i < iReadLength; i++)
            {
                SortedSet<int> e1 = new();
                for (int j = 0, jReadLength = reader.ReadInt32(); j < jReadLength; j++)
                    e1.Add(reader.ReadInt32());
                result.Add(e1);
            }
            return result;
        }

        public async Task<SortedSet<int>> GetSizeMismatchTargetFileIndices(CancellationToken? cancellationToken = null)
        {
            using var reader = await WaitForResult(GetRequestCreator(WorkerInboundOpcode.GetSizeMismatchTargetFileIndices, cancellationToken), cancellationToken, 30000, false);
            SortedSet<int> result = new();
            for (int i = 0, readIndex = reader.ReadInt32(); i < readIndex; i++)
                result.Add(reader.ReadInt32());
            return result;
        }

        public async Task SetWorkerProcessPriority(ProcessPriorityClass subprocessPriority, CancellationToken? cancellationToken = null)
        {
            var writer = GetRequestCreator(WorkerInboundOpcode.SetWorkerProcessPriority, cancellationToken);
            writer.Write((int)subprocessPriority);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task MoveFile(string sourceFile, string targetFile, CancellationToken? cancellationToken = null) {
            var writer = GetRequestCreator(WorkerInboundOpcode.MoveFile, cancellationToken);
            writer.Write(sourceFile);
            writer.Write(targetFile);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task CreateDirectory(string dir, CancellationToken? cancellationToken = null) {
            var writer = GetRequestCreator(WorkerInboundOpcode.CreateDirectory, cancellationToken);
            writer.Write(dir);
            await WaitForResult(writer, cancellationToken);
        }

        public async Task RemoveDirectory(string dir, bool recursive = false, CancellationToken? cancellationToken = null) {
            var writer = GetRequestCreator(WorkerInboundOpcode.RemoveDirectory, cancellationToken);
            writer.Write(dir);
            writer.Write(recursive);
            await WaitForResult(writer, cancellationToken);
        }

        public class WorkerSubprocessBody : IDisposable
        {
            private readonly object progressUpdateSync = new();
            private readonly Process parentProcess;
            private readonly RpcBuffer subprocessBuffer;
            private readonly Dictionary<int, CancellationTokenSource> cancellationTokenSources = new();
            private IndexedZiPatchInstaller instance = null;
            private long progressUpdateCounter = 0;

            public WorkerSubprocessBody(int monitorProcessId, string channelName)
            {
                this.parentProcess = Process.GetProcessById(monitorProcessId);
                this.subprocessBuffer = new RpcBuffer(channelName, async (ulong _, byte[] data) =>
                {
                    using var reader = new BinaryReader(new MemoryStream(data));
                    var cancelSourceId = reader.ReadInt32();
                    CancellationToken? cancelToken = null;
                    if (cancelSourceId != -1)
                    {
                        this.cancellationTokenSources[cancelSourceId] = new CancellationTokenSource();
                        cancelToken = this.cancellationTokenSources[cancelSourceId].Token;
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
                                lock (this.cancellationTokenSources)
                                {
                                    if (this.cancellationTokenSources.TryGetValue(reader.ReadInt32(), out var cts))
                                        cts.Cancel();
                                }
                                break;

                            case WorkerInboundOpcode.Construct:
                                this.instance?.Dispose();
                                this.instance = new(new IndexedZiPatchIndex(reader, false))
                                {
                                    ProgressReportInterval = reader.ReadInt32(),
                                };
                                this.instance.OnInstallProgress += OnInstallProgressUpdate;
                                this.instance.OnVerifyProgress += OnVerifyProgressUpdate;
                                break;

                            case WorkerInboundOpcode.DisposeAndExit:
                                this.instance?.Dispose();
                                this.instance = null;
                                Environment.Exit(0);
                                break;

                            case WorkerInboundOpcode.VerifyFiles:
                                await this.instance.VerifyFiles(reader.ReadBoolean(), reader.ReadInt32(), cancelToken);
                                break;

                            case WorkerInboundOpcode.MarkFileAsMissing:
                                this.instance.MarkFileAsMissing(reader.ReadInt32());
                                break;

                            case WorkerInboundOpcode.SetTargetStreamFromPathReadOnly:
                                this.instance.SetTargetStreamForRead(reader.ReadInt32(), new FileStream(reader.ReadString(), FileMode.Open, FileAccess.Read));
                                break;

                            case WorkerInboundOpcode.SetTargetStreamFromPathReadWrite:
                                this.instance.SetTargetStreamForWriteFromFile(reader.ReadInt32(), new FileInfo(reader.ReadString()));
                                break;

                            case WorkerInboundOpcode.SetTargetStreamsFromPathReadOnly:
                                this.instance.SetTargetStreamsFromPathReadOnly(reader.ReadString());
                                break;

                            case WorkerInboundOpcode.SetTargetStreamsFromPathReadWriteForMissingFiles:
                                this.instance.SetTargetStreamsFromPathReadWriteForMissingFiles(reader.ReadString());
                                break;

                            case WorkerInboundOpcode.RepairNonPatchData:
                                await this.instance.RepairNonPatchData(cancelToken);
                                break;

                            case WorkerInboundOpcode.WriteVersionFiles:
                                this.instance.WriteVersionFiles(reader.ReadString());
                                break;

                            case WorkerInboundOpcode.QueueInstallFromUrl:
                                this.instance.QueueInstall(reader.ReadInt32(), reader.ReadString(), reader.ReadString(), reader.ReadInt32());
                                break;

                            case WorkerInboundOpcode.QueueInstallFromLocalFile:
                                this.instance.QueueInstall(reader.ReadInt32(), new FileInfo(reader.ReadString()), reader.ReadInt32());
                                break;

                            case WorkerInboundOpcode.Install:
                                await this.instance.Install(reader.ReadInt32(), cancelToken);
                                break;

                            case WorkerInboundOpcode.GetMissingPartIndicesPerPatch:
                                writer.Write(this.instance.MissingPartIndicesPerPatch.Count);
                                foreach (var e1 in this.instance.MissingPartIndicesPerPatch)
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
                                writer.Write(this.instance.MissingPartIndicesPerTargetFile.Count);
                                foreach (var e1 in this.instance.MissingPartIndicesPerTargetFile)
                                {
                                    writer.Write(e1.Count);
                                    foreach (var e2 in e1)
                                        writer.Write(e2);
                                }
                                break;

                            case WorkerInboundOpcode.GetSizeMismatchTargetFileIndices:
                                writer.Write(this.instance.SizeMismatchTargetFileIndices.Count);
                                foreach (var e1 in this.instance.SizeMismatchTargetFileIndices)
                                    writer.Write(e1);
                                break;

                            case WorkerInboundOpcode.SetWorkerProcessPriority:
                                Process.GetCurrentProcess().PriorityClass = (ProcessPriorityClass)reader.ReadInt32();
                                break;

                            case WorkerInboundOpcode.MoveFile:
                                {
                                    var sourceFileName = reader.ReadString();
                                    var targetFileName = reader.ReadString();

                                    var sourceParentDir = new DirectoryInfo(Path.GetDirectoryName(sourceFileName));
                                    var targetParentDir = new DirectoryInfo(Path.GetDirectoryName(targetFileName));

                                    targetParentDir.Create();
                                    new FileInfo(sourceFileName).MoveTo(targetFileName);

                                    if (!sourceParentDir.GetFileSystemInfos().Any())
                                        sourceParentDir.Delete(false);
                                    break;
                                }

                            case WorkerInboundOpcode.CreateDirectory:
                                new DirectoryInfo(reader.ReadString()).Create();
                                break;

                            case WorkerInboundOpcode.RemoveDirectory:
                                {
                                    var dir = new DirectoryInfo(reader.ReadString());
                                    var recursive = reader.ReadBoolean();
                                    dir.Delete(recursive);
                                    break;
                                }

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
                            this.cancellationTokenSources.Remove(cancelSourceId);
                    }
                    return ms.ToArray();
                });
            }

            private void OnInstallProgressUpdate(int index, long progress, long max, IndexedZiPatchInstaller.InstallTaskState state)
            {
                lock (this.progressUpdateSync)
                {
                    var ms = new MemoryStream();
                    var writer = new BinaryWriter(ms);
                    writer.Write((int)WorkerOutboundOpcode.UpdateInstallProgress);
                    writer.Write(this.progressUpdateCounter);
                    writer.Write(index);
                    writer.Write(progress);
                    writer.Write(max);
                    writer.Write((int)state);
                    this.progressUpdateCounter += 1;
                    this.subprocessBuffer.RemoteRequest(ms.ToArray());
                }
            }

            private void OnVerifyProgressUpdate(int index, long progress, long max)
            {
                lock (this.progressUpdateSync)
                {
                    var ms = new MemoryStream();
                    var writer = new BinaryWriter(ms);
                    writer.Write((int)WorkerOutboundOpcode.UpdateVerifyProgress);
                    writer.Write(this.progressUpdateCounter);
                    writer.Write(index);
                    writer.Write(progress);
                    writer.Write(max);
                    this.progressUpdateCounter += 1;
                    this.subprocessBuffer.RemoteRequest(ms.ToArray());
                }
            }

            public void Dispose()
            {
                this.subprocessBuffer.Dispose();
                this.instance?.Dispose();
            }

            public void Run()
            {
                this.parentProcess.WaitForExit();
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
            UpdateInstallProgress,
            UpdateVerifyProgress,
        }

        private enum WorkerInboundOpcode : int
        {
            CancelTask,
            Construct,
            DisposeAndExit,
            VerifyFiles,
            MarkFileAsMissing,
            SetTargetStreamFromPathReadOnly,
            SetTargetStreamFromPathReadWrite,
            SetTargetStreamsFromPathReadOnly,
            SetTargetStreamsFromPathReadWriteForMissingFiles,
            RepairNonPatchData,
            WriteVersionFiles,
            QueueInstallFromUrl,
            QueueInstallFromLocalFile,
            Install,
            GetMissingPartIndicesPerPatch,
            GetMissingPartIndicesPerTargetFile,
            GetSizeMismatchTargetFileIndices,
            SetWorkerProcessPriority,
            MoveFile,
            CreateDirectory,
            RemoveDirectory,
        }

        public static void Test()
        {
            Task.Run(async () =>
            {
                // Cancel in 15 secs
                var cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = cancellationTokenSource.Token;

                var availableSourceUrls = new Dictionary<string, string>() {
                    {"boot:D2013.06.18.0000.0000.patch", "http://patch-dl.ffxiv.com/boot/2b5cbc63/D2013.06.18.0000.0000.patch"},
                    {"boot:D2021.11.16.0000.0001.patch", "http://patch-dl.ffxiv.com/boot/2b5cbc63/D2021.11.16.0000.0001.patch"},
                };
                var maxConcurrentConnectionsForPatchSet = 1;

                // var baseDir = @"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn";
                var baseDir = @"Z:\tgame";
                var rootAndPatchPairs = new List<Tuple<string, string>>() {
                    Tuple.Create(@$"{baseDir}\boot", @"Z:\patch-dl.ffxiv.com\boot\2b5cbc63\D2021.11.16.0000.0001.patch.index"),
                };

                // Run verifier as subprocess
                // using var verifier = new IndexedZiPatchIndexRemoteInstaller(System.Reflection.Assembly.GetExecutingAssembly().Location, true);
                // Run verifier as another thread
                using var verifier = new IndexedZiPatchIndexRemoteInstaller(null, true);

                foreach (var (gameRootPath, patchIndexFilePath) in rootAndPatchPairs)
                {
                    var patchIndex = new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));

                    await verifier.ConstructFromPatchFile(patchIndex, 1000);

                    void ReportCheckProgress(int index, long progress, long max)
                    {
                        Log.Information("[{0}/{1}] Checking file {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", index + 1, patchIndex.Length, patchIndex[Math.Min(index, patchIndex.Length - 1)].RelativePath, progress / 1048576.0, max / 1048576.0, 100.0 * progress / max);
                    }

                    void ReportInstallProgress(int index, long progress, long max, IndexedZiPatchInstaller.InstallTaskState state)
                    {
                        Log.Information("[{0}/{1}] {2} {3}... {4:0.00}/{5:0.00}MB ({6:00.00}%)", index + 1, patchIndex.Sources.Count, state, patchIndex.Sources[Math.Min(index, patchIndex.Sources.Count - 1)], progress / 1048576.0, max / 1048576.0, 100.0 * progress / max);
                    }

                    verifier.OnVerifyProgress += ReportCheckProgress;
                    verifier.OnInstallProgress += ReportInstallProgress;

                    for (var attemptIndex = 0; attemptIndex < 5; attemptIndex++)
                    {
                        await verifier.SetTargetStreamsFromPathReadOnly(gameRootPath);
                        // TODO: check one at a time if random access is slow?
                        await verifier.VerifyFiles(attemptIndex > 0, Environment.ProcessorCount, cancellationToken);

                        var missingPartIndicesPerTargetFile = await verifier.GetMissingPartIndicesPerTargetFile();
                        if (missingPartIndicesPerTargetFile.All(x => !x.Any()))
                            break;

                        var missingPartIndicesPerPatch = await verifier.GetMissingPartIndicesPerPatch();
                        await verifier.SetTargetStreamsFromPathReadWriteForMissingFiles(gameRootPath);
                        var prefix = patchIndex.ExpacVersion == IndexedZiPatchIndex.EXPAC_VERSION_BOOT ? "boot:" : $"ex{patchIndex.ExpacVersion}:";
                        for (var i = 0; i < patchIndex.Sources.Count; i++)
                        {
                            if (!missingPartIndicesPerPatch[i].Any())
                                continue;

                            await verifier.QueueInstall(i, new Uri(availableSourceUrls[prefix + patchIndex.Sources[i]]), null, maxConcurrentConnectionsForPatchSet);
                            // await verifier.QueueInstall(i, new FileInfo(availableSourceUrls[prefix + patchIndex.Sources[i]].Replace("http:/", "Z:")));
                        }
                        await verifier.Install(maxConcurrentConnectionsForPatchSet, cancellationToken);
                        await verifier.WriteVersionFiles(gameRootPath);
                    }
                    verifier.OnVerifyProgress -= ReportCheckProgress;
                    verifier.OnInstallProgress -= ReportInstallProgress;
                }
            }).Wait();
        }
    }
}