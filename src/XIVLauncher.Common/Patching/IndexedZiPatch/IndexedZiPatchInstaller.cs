using Serilog;
using System;
using System.Collections.Generic;

#if WIN32
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
#endif

using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{

    public class IndexedZiPatchInstaller : IDisposable
    {
        public readonly IndexedZiPatchIndex Index;
        public readonly List<SortedSet<Tuple<int, int>>> MissingPartIndicesPerPatch = new();
        public readonly List<SortedSet<int>> MissingPartIndicesPerTargetFile = new();
        public readonly SortedSet<int> SizeMismatchTargetFileIndices = new();

        public int ProgressReportInterval = 250;
        private readonly List<Stream> targetStreams = new();
        private readonly List<object> targetLocks = new();

        public enum InstallTaskState
        {
            NotStarted,
            WaitingForReattempt,
            Connecting,
            Working,
            Finishing,
            Done,
            Error,
        }

        public delegate void OnCorruptionFoundDelegate(IndexedZiPatchPartLocator part, IndexedZiPatchPartLocator.VerifyDataResult result);
        public delegate void OnVerifyProgressDelegate(int targetIndex, long progress, long max);
        public delegate void OnInstallProgressDelegate(int sourceIndex, long progress, long max, InstallTaskState state);

        public event OnCorruptionFoundDelegate OnCorruptionFound;
        public event OnVerifyProgressDelegate OnVerifyProgress;
        public event OnInstallProgressDelegate OnInstallProgress;

        // Definitions taken from PInvoke.net (with some changes)
        // ReSharper disable InconsistentNaming

#if WIN32
        private static class PInvoke
        {
            #region Constants

            public const UInt32 TOKEN_QUERY = 0x0008;
            public const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;

            public const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;

            public const UInt32 ERROR_NOT_ALL_ASSIGNED = 0x514;

            #endregion

            #region Structures

            [StructLayout(LayoutKind.Sequential)]
            public struct LUID
            {
                public UInt32 LowPart;
                public Int32 HighPart;
            }

            public struct LUID_AND_ATTRIBUTES
            {
                public LUID Luid;
                public UInt32 Attributes;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct TOKEN_PRIVILEGES
            {
                public UInt32 PrivilegeCount;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
                public LUID_AND_ATTRIBUTES[] Privileges;
            }

            #endregion

            #region Methods

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetFileValidData(IntPtr hFile, long ValidDataLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool OpenProcessToken(
                IntPtr ProcessHandle,
                UInt32 DesiredAccess,
                out IntPtr TokenHandle);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref LUID lpLuid);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool AdjustTokenPrivileges(
                IntPtr TokenHandle,
                bool DisableAllPrivileges,
                ref TOKEN_PRIVILEGES NewState,
                int BufferLengthInBytes,
                IntPtr PreviousState,
                IntPtr ReturnLengthInBytes);

            #endregion

            #region Utilities

            // https://docs.microsoft.com/en-us/windows/win32/secauthz/enabling-and-disabling-privileges-in-c--
            public static void SetPrivilege(IntPtr hToken, string lpszPrivilege, bool bEnablePrivilege)
            {
                LUID luid = new();
                if (!LookupPrivilegeValue(null, lpszPrivilege, ref luid))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "LookupPrivilegeValue failed.");

                TOKEN_PRIVILEGES tp = new()
                {
                    PrivilegeCount = 1,
                    Privileges = new LUID_AND_ATTRIBUTES[] {
                        new LUID_AND_ATTRIBUTES{
                            Luid = luid,
                            Attributes = bEnablePrivilege ? SE_PRIVILEGE_ENABLED : 0,
                        }
                    },
                };
                if (!AdjustTokenPrivileges(hToken, false, ref tp, Marshal.SizeOf(tp), IntPtr.Zero, IntPtr.Zero))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "AdjustTokenPrivileges failed.");

                if (Marshal.GetLastWin32Error() == ERROR_NOT_ALL_ASSIGNED)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The token does not have the specified privilege.");
            }

            public static void SetCurrentPrivilege(string lpszPrivilege, bool bEnablePrivilege)
            {
                if (!OpenProcessToken(Process.GetCurrentProcess().SafeHandle.DangerousGetHandle(), TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, out var hToken))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                try
                {
                    SetPrivilege(hToken, lpszPrivilege, bEnablePrivilege);
                }
                finally
                {
                    CloseHandle(hToken);
                }
            }

            #endregion
        }
        // ReSharper restore once InconsistentNaming
#endif

        public IndexedZiPatchInstaller(IndexedZiPatchIndex def)
        {
            Index = def;
            foreach (var _ in def.Targets)
            {
                MissingPartIndicesPerTargetFile.Add(new());
                this.targetStreams.Add(null);
                this.targetLocks.Add(new());
            }
            foreach (var _ in def.Sources)
                MissingPartIndicesPerPatch.Add(new());
        }

        public async Task VerifyFiles(bool refine = false, int concurrentCount = 8, CancellationToken? cancellationToken = null)
        {
            CancellationTokenSource localCancelSource = new();

            if (cancellationToken.HasValue)
                cancellationToken.Value.Register(() => localCancelSource?.Cancel());

            SizeMismatchTargetFileIndices.Clear();
            foreach (var l in MissingPartIndicesPerPatch)
                l.Clear();

            List<Task> verifyTasks = new();
            try
            {
                long progressCounter = 0;
                long progressMax = refine ? MissingPartIndicesPerTargetFile.Select((x, i) => x.Select(y => Index[i][y].TargetSize).Sum()).Sum() : Index.Targets.Select((x, i) => this.targetStreams[i] == null ? 0 : x.FileSize).Sum();

                Queue<int> pendingTargetIndices = new();
                for (int i = 0; i < Index.Length; i++)
                    pendingTargetIndices.Enqueue(i);

                Task progressReportTask = null;
                while (verifyTasks.Any() || pendingTargetIndices.Any())
                {
                    localCancelSource.Token.ThrowIfCancellationRequested();

                    while (pendingTargetIndices.Any() && verifyTasks.Count < concurrentCount)
                    {
                        var targetIndex = pendingTargetIndices.Dequeue();
                        var stream = this.targetStreams[targetIndex];
                        if (stream == null)
                            continue;

                        var file = Index[targetIndex];
                        if (stream.Length != file.FileSize)
                            SizeMismatchTargetFileIndices.Add(targetIndex);

                        verifyTasks.Add(Task.Run(() =>
                        {
                            List<int> targetPartIndicesToCheck;
                            if (refine)
                            {
                                targetPartIndicesToCheck = MissingPartIndicesPerTargetFile[targetIndex].ToList();
                                MissingPartIndicesPerTargetFile[targetIndex].Clear();
                            }
                            else
                            {
                                targetPartIndicesToCheck = new();
                                for (var partIndex = 0; partIndex < file.Count; ++partIndex)
                                    targetPartIndicesToCheck.Add(partIndex);
                            }
                            foreach (var partIndex in targetPartIndicesToCheck)
                            {
                                localCancelSource.Token.ThrowIfCancellationRequested();

                                var verifyResult = file[partIndex].Verify(stream);
                                lock (verifyTasks)
                                {
                                    progressCounter += file[partIndex].TargetSize;
                                    switch (verifyResult)
                                    {
                                        case IndexedZiPatchPartLocator.VerifyDataResult.Pass:
                                            break;

                                        case IndexedZiPatchPartLocator.VerifyDataResult.FailUnverifiable:
                                            throw new Exception($"{file.RelativePath}:{file[partIndex].TargetOffset}:{file[partIndex].TargetEnd}: Should not happen; unverifiable due to insufficient source data");

                                        case IndexedZiPatchPartLocator.VerifyDataResult.FailNotEnoughData:
                                        case IndexedZiPatchPartLocator.VerifyDataResult.FailBadData:
                                            MissingPartIndicesPerTargetFile[file[partIndex].TargetIndex].Add(partIndex);
                                            OnCorruptionFound?.Invoke(file[partIndex], verifyResult);
                                            break;
                                    }
                                }
                            }
                        }));
                    }

                    if (progressReportTask == null || progressReportTask.IsCompleted)
                    {
                        progressReportTask = Task.Delay(ProgressReportInterval, localCancelSource.Token);
                        OnVerifyProgress?.Invoke(Math.Max(0, Index.Length - pendingTargetIndices.Count - verifyTasks.Count - 1), progressCounter, progressMax);
                    }

                    verifyTasks.Add(progressReportTask);
                    await Task.WhenAny(verifyTasks);
                    verifyTasks.RemoveAt(verifyTasks.Count - 1);
                    if (verifyTasks.FirstOrDefault(x => x.IsFaulted) is Task x)
                        throw x.Exception;
                    verifyTasks.RemoveAll(x => x.IsCompleted);
                }

                for (var targetIndex = 0; targetIndex < Index.Length; targetIndex++)
                {
                    foreach (var partIndex in MissingPartIndicesPerTargetFile[targetIndex])
                    {
                        var part = Index[targetIndex][partIndex];
                        if (part.IsFromSourceFile)
                            MissingPartIndicesPerPatch[part.SourceIndex].Add(Tuple.Create(targetIndex, partIndex));
                    }
                }
            }
            finally
            {
                localCancelSource.Cancel();
                foreach (var task in verifyTasks)
                {
                    if (task.IsCompleted)
                        continue;
                    try
                    {
                        await task;
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }
                localCancelSource.Dispose();
                localCancelSource = null;
            }
        }

        public void MarkFileAsMissing(int targetIndex)
        {
            var file = Index[targetIndex];
            for (var i = 0; i < file.Count; ++i)
                MissingPartIndicesPerTargetFile[targetIndex].Add(i);
        }

        public void SetTargetStreamForRead(int targetIndex, Stream targetStream)
        {
            if (!targetStream.CanRead || !targetStream.CanSeek)
                throw new ArgumentException("Target stream must be readable and seekable.");

            this.targetStreams[targetIndex] = targetStream;
        }

        public void SetTargetStreamForWriteFromFile(int targetIndex, FileInfo fileInfo, bool useSetFileValidData = false)
        {
            var file = Index[targetIndex];
            fileInfo.Directory.Create();
            var stream = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite);

            if (stream.Length != file.FileSize)
            {
                stream.Seek(file.FileSize, SeekOrigin.Begin);
                stream.SetLength(file.FileSize);

#if WIN32
                if (useSetFileValidData && !PInvoke.SetFileValidData(stream.SafeFileHandle.DangerousGetHandle(), file.FileSize))
                    Log.Information($"Unable to apply SetFileValidData on file {fileInfo.FullName} (error code {Marshal.GetLastWin32Error()})");
#endif
            }

            this.targetStreams[targetIndex] = stream;
        }

        public void SetTargetStreamsFromPathReadOnly(string rootPath)
        {
            Dispose();
            for (var i = 0; i < Index.Length; i++)
            {
                var file = Index[i];
                var fileInfo = new FileInfo(Path.Combine(rootPath, file.RelativePath));
                if (fileInfo.Exists)
                    SetTargetStreamForRead(i, new FileStream(Path.Combine(rootPath, file.RelativePath), FileMode.Open, FileAccess.Read));
                else
                    MarkFileAsMissing(i);
            }
        }

        public void SetTargetStreamsFromPathReadWriteForMissingFiles(string rootPath)
        {
            Dispose();

#if WIN32
            var useSetFileValidData = true;
            try
            {
                PInvoke.SetCurrentPrivilege("SeManageVolumePrivilege", true);
            }
            catch (Win32Exception e)
            {
                Log.Information(e, "Unable to obtain SeManageVolumePrivilege; not using SetFileValidData.");
                useSetFileValidData = false;
            }
#else
            var useSetFileValidData = false;
#endif

            for (var i = 0; i < Index.Length; i++)
            {
                if (MissingPartIndicesPerTargetFile[i].Count == 0 && !SizeMismatchTargetFileIndices.Contains(i))
                    continue;

                var file = Index[i];
                var fileInfo = new FileInfo(Path.Combine(rootPath, file.RelativePath));
                SetTargetStreamForWriteFromFile(i, fileInfo, useSetFileValidData);
            }
        }

        private void WriteToTarget(int targetIndex, long targetOffset, byte[] buffer, int offset, int count)
        {
            var target = this.targetStreams[targetIndex];
            if (target == null)
                return;

            lock (this.targetLocks[targetIndex])
            {
                target.Seek(targetOffset, SeekOrigin.Begin);
                target.Write(buffer, offset, count);
                target.Flush();
            }
        }

        public async Task RepairNonPatchData(CancellationToken? cancellationToken = null)
        {
            await Task.Run(() =>
            {
                for (int i = 0, length = Index.Length; i < length; i++)
                {
                    if (cancellationToken.HasValue)
                        cancellationToken.Value.ThrowIfCancellationRequested();

                    var file = Index[i];
                    foreach (var partIndex in MissingPartIndicesPerTargetFile[i])
                    {
                        if (cancellationToken.HasValue)
                            cancellationToken.Value.ThrowIfCancellationRequested();

                        var part = file[partIndex];
                        if (part.IsFromSourceFile)
                            continue;

                        using var buffer = ReusableByteBufferManager.GetBuffer(part.TargetSize);
                        part.ReconstructWithoutSourceData(buffer.Buffer);
                        WriteToTarget(i, part.TargetOffset, buffer.Buffer, 0, (int)part.TargetSize);
                    }
                }
            });
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
            public readonly IndexedZiPatchIndex Index;
            public readonly IndexedZiPatchInstaller Installer;
            public readonly int SourceIndex;
            public readonly List<Tuple<int, int>> TargetPartIndices;
            public readonly List<Tuple<int, int>> CompletedTargetPartIndices = new();
            public InstallTaskState State { get; protected set; } = InstallTaskState.NotStarted;

            public InstallTaskConfig(IndexedZiPatchInstaller installer, int sourceIndex, IEnumerable<Tuple<int, int>> targetPartIndices)
            {
                Index = installer.Index;
                Installer = installer;
                SourceIndex = sourceIndex;
                TargetPartIndices = targetPartIndices.ToList();
            }

            public abstract Task Repair(CancellationToken cancellationToken);

            public virtual void Dispose() { }
        }

        public class HttpInstallTaskConfig : InstallTaskConfig
        {
            private static readonly int[] ReattemptWait = new int[] { 0, 500, 1000, 2000, 3000, 5000, 10000, 15000, 20000, 25000, 30000, 45000, 60000 };
            private const int MERGED_GAP_DOWNLOAD = 512;

            public readonly string SourceUrl;
            private readonly HttpClient client = new();
            private readonly List<long> targetPartOffsets;
            private readonly string sid;

            public HttpInstallTaskConfig(IndexedZiPatchInstaller installer, int sourceIndex, IEnumerable<Tuple<int, int>> targetPartIndices, string sourceUrl, string sid)
                : base(installer, sourceIndex, targetPartIndices)
            {
                SourceUrl = sourceUrl;
                this.sid = sid;
                TargetPartIndices.Sort((x, y) => Index[x.Item1][x.Item2].SourceOffset.CompareTo(Index[y.Item1][y.Item2].SourceOffset));
                this.targetPartOffsets = TargetPartIndices.Select(x => Index[x.Item1][x.Item2].SourceOffset).ToList();

                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    ProgressMax += Index[targetIndex][partIndex].TargetSize;
            }

            private MultipartResponseHandler multipartResponse = null;

            private async Task<MultipartResponseHandler.MultipartPartStream> GetNextStream(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (this.multipartResponse != null)
                {
                    var stream1 = await this.multipartResponse.NextPart(cancellationToken);
                    if (stream1 != null)
                        return stream1;

                    this.multipartResponse?.Dispose();
                    this.multipartResponse = null;
                }

                var offsets = new List<Tuple<long, long>>();
                offsets.Clear();
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    offsets.Add(Tuple.Create(Index[targetIndex][partIndex].SourceOffset, Math.Min(Index.GetSourceLastPtr(SourceIndex), Index[targetIndex][partIndex].MaxSourceEnd)));
                offsets.Sort();

                for (int i = 1; i < offsets.Count; i++)
                {
                    if (offsets[i].Item1 - offsets[i - 1].Item2 >= MERGED_GAP_DOWNLOAD)
                        continue;
                    offsets[i - 1] = Tuple.Create(offsets[i - 1].Item1, Math.Max(offsets[i - 1].Item2, offsets[i].Item2));
                    offsets.RemoveAt(i);
                    i -= 1;
                }

                using HttpRequestMessage req = new(HttpMethod.Get, SourceUrl);
                req.Headers.Range = new();
                req.Headers.Range.Unit = "bytes";
                foreach (var (rangeFrom, rangeToExclusive) in offsets)
                    req.Headers.Range.Ranges.Add(new(rangeFrom, rangeToExclusive + 1));
                if (this.sid != null)
                    req.Headers.Add("X-Patch-Unique-Id", this.sid);
                req.Headers.Add("User-Agent", Constants.PatcherUserAgent);
                req.Headers.Add("Connection", "Keep-Alive");

                try
                {
                    var resp = await this.client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    this.multipartResponse = new MultipartResponseHandler(resp);
                }
                catch (HttpRequestException e)
                {
                    throw new IOException($"Failed to send request to {SourceUrl} with {offsets.Count} range element(s).", e);
                }

                var stream2 = await this.multipartResponse.NextPart(cancellationToken);
                if (stream2 == null)
                    throw new EndOfStreamException("Encountered premature end of stream");
                return stream2;
            }

            public override async Task Repair(CancellationToken cancellationToken)
            {
                for (int failedCount = 0; TargetPartIndices.Any() && failedCount < ReattemptWait.Length;)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        State = InstallTaskState.WaitingForReattempt;
                        await Task.Delay(ReattemptWait[failedCount], cancellationToken);

                        State = InstallTaskState.Connecting;
                        var stream = await GetNextStream(cancellationToken);

                        State = InstallTaskState.Working;
                        while (this.targetPartOffsets.Any())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var (targetIndex, partIndex) = TargetPartIndices.First();
                            var part = Index[targetIndex][partIndex];

                            if (Math.Min(part.MaxSourceEnd, Index.GetSourceLastPtr(SourceIndex)) > stream.OriginEnd)
                                break;

                            using var targetBuffer = ReusableByteBufferManager.GetBuffer(part.TargetSize);
                            part.Reconstruct(stream, targetBuffer.Buffer);
                            Installer.WriteToTarget(part.TargetIndex, part.TargetOffset, targetBuffer.Buffer, 0, (int)part.TargetSize);
                            failedCount = 0;

                            ProgressValue += part.TargetSize;
                            CompletedTargetPartIndices.Add(TargetPartIndices.First());
                            TargetPartIndices.RemoveAt(0);
                            this.targetPartOffsets.RemoveAt(0);
                        }
                    }
                    catch (IOException ex)
                    {
                        if (failedCount >= 8)
                            Log.Error(ex, "HttpInstallTask failed");
                        else
                            Log.Warning(ex, "HttpInstallTask reattempting");

                        failedCount++;
                        if (failedCount == ReattemptWait.Length)
                        {
                            State = InstallTaskState.Error;
                            throw;
                        }
                    }
                    catch (Exception)
                    {
                        State = InstallTaskState.Error;
                        throw;
                    }
                }

                State = InstallTaskState.Done;
            }

            public override void Dispose()
            {
                this.multipartResponse?.Dispose();
                this.client.Dispose();
                base.Dispose();
            }
        }

        public class StreamInstallTaskConfig : InstallTaskConfig
        {
            public readonly Stream SourceStream;
            public readonly IList<Tuple<long, long>> SourceOffsets;

            public StreamInstallTaskConfig(IndexedZiPatchInstaller installer, int sourceIndex, IEnumerable<Tuple<int, int>> targetPartIndices, Stream sourceStream)
                : base(installer, sourceIndex, targetPartIndices)
            {
                SourceStream = sourceStream;
                long totalTargetSize = 0;
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    totalTargetSize += Index[targetIndex][partIndex].TargetSize;
                ProgressMax = totalTargetSize;
            }

            public override async Task Repair(CancellationToken cancellationToken)
            {
                State = InstallTaskState.Working;
                try
                {
                    await Task.Run(() =>
                    {
                        while (TargetPartIndices.Any())
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var (targetIndex, partIndex) = TargetPartIndices.First();
                            var part = Index[targetIndex][partIndex];

                            using var buffer = ReusableByteBufferManager.GetBuffer(part.TargetSize);
                            part.Reconstruct(SourceStream, buffer.Buffer);
                            Installer.WriteToTarget(part.TargetIndex, part.TargetOffset, buffer.Buffer, 0, (int)part.TargetSize);

                            ProgressValue += part.TargetSize;
                            CompletedTargetPartIndices.Add(TargetPartIndices.First());
                            TargetPartIndices.RemoveAt(0);
                        }
                    });
                    State = InstallTaskState.Done;
                }
                catch (Exception)
                {
                    State = InstallTaskState.Error;
                }
            }

            public override void Dispose()
            {
                SourceStream.Dispose();
                base.Dispose();
            }
        }

        private readonly List<InstallTaskConfig> installTaskConfigs = new();

        public void QueueInstall(int sourceIndex, string sourceUrl, string sid, ISet<Tuple<int, int>> targetPartIndices)
        {
            if (targetPartIndices.Any())
                this.installTaskConfigs.Add(new HttpInstallTaskConfig(this, sourceIndex, targetPartIndices, sourceUrl, sid == "" ? null : sid));
        }

        public void QueueInstall(int sourceIndex, string sourceUrl, string sid, int splitBy = 8)
        {
            const int MAX_DOWNLOAD_PER_REQUEST = 256 * 1024 * 1024;

            var indices = MissingPartIndicesPerPatch[sourceIndex].ToList();
            var indicesPerRequest = (int)Math.Ceiling(1.0 * indices.Count / splitBy);
            for (int j = 0; j < indices.Count;)
            {
                SortedSet<Tuple<int, int>> targetPartIndices = new();
                long size = 0;
                for (; j < indices.Count && targetPartIndices.Count < indicesPerRequest && size < MAX_DOWNLOAD_PER_REQUEST; ++j)
                {
                    targetPartIndices.Add(indices[j]);
                    size += Index[indices[j].Item1][indices[j].Item2].MaxSourceSize;
                }
                QueueInstall(sourceIndex, sourceUrl, sid, targetPartIndices);
            }
        }

        public void QueueInstall(int sourceIndex, Stream stream, ISet<Tuple<int, int>> targetPartIndices)
        {
            if (targetPartIndices.Any())
                this.installTaskConfigs.Add(new StreamInstallTaskConfig(this, sourceIndex, targetPartIndices, stream));
        }

        public void QueueInstall(int sourceIndex, FileInfo file, ISet<Tuple<int, int>> targetPartIndices)
        {
            if (targetPartIndices.Any())
                QueueInstall(sourceIndex, file.OpenRead(), targetPartIndices);
        }

        public void QueueInstall(int sourceIndex, FileInfo file, int splitBy = 8)
        {
            var indices = MissingPartIndicesPerPatch[sourceIndex];
            var indicesPerRequest = (int)Math.Ceiling(1.0 * indices.Count / splitBy);
            for (int j = 0; j < indices.Count; j += indicesPerRequest)
                QueueInstall(sourceIndex, file, new HashSet<Tuple<int, int>>(indices.Skip(j).Take(Math.Min(indicesPerRequest, indices.Count - j)))); // This was .ToHashSet(), but .NET Standard 2.0 doesn't have it
        }

        public async Task Install(int concurrentCount, CancellationToken? cancellationToken = null)
        {
            if (!this.installTaskConfigs.Any())
            {
                await RepairNonPatchData();
                return;
            }

            long progressMax = this.installTaskConfigs.Select(x => x.ProgressMax).Sum();

            CancellationTokenSource localCancelSource = new();

            if (cancellationToken.HasValue)
                cancellationToken.Value.Register(() => localCancelSource?.Cancel());

            Task progressReportTask = null;
            Queue<InstallTaskConfig> pendingTaskConfigs = new();
            foreach (var x in this.installTaskConfigs)
                pendingTaskConfigs.Enqueue(x);

            List<Task> runningTasks = new();

            try
            {
                while (pendingTaskConfigs.Any() || runningTasks.Any())
                {
                    localCancelSource.Token.ThrowIfCancellationRequested();

                    while (pendingTaskConfigs.Any() && runningTasks.Count < concurrentCount)
                        runningTasks.Add(pendingTaskConfigs.Dequeue().Repair(localCancelSource.Token));

                    OnInstallProgress?.Invoke(
                        this.installTaskConfigs[Math.Max(0, this.installTaskConfigs.Count - pendingTaskConfigs.Count - runningTasks.Count - 1)].SourceIndex,
                        this.installTaskConfigs.Select(x => x.ProgressValue).Sum(),
                        progressMax,
                        this.installTaskConfigs.Where(x => x.State < InstallTaskState.Finishing).Select(x => x.State).Max()
                        );

                    if (progressReportTask == null || progressReportTask.IsCompleted)
                        progressReportTask = Task.Delay(ProgressReportInterval, localCancelSource.Token);
                    runningTasks.Add(progressReportTask);
                    await Task.WhenAny(runningTasks);
                    runningTasks.RemoveAt(runningTasks.Count - 1);

                    if (runningTasks.FirstOrDefault(x => x.IsFaulted) is Task x)
                        throw x.Exception;
                    runningTasks.RemoveAll(x => x.IsCompleted);
                }

                OnInstallProgress?.Invoke(this.installTaskConfigs.Last().SourceIndex, progressMax, progressMax, InstallTaskState.Finishing);
                await RepairNonPatchData();
            }
            finally
            {
                localCancelSource.Cancel();
                foreach (var task in runningTasks)
                {
                    if (task.IsCompleted)
                        continue;
                    try
                    {
                        await task;
                    }
                    catch (Exception)
                    {
                        // ignore
                    }
                }
                localCancelSource.Dispose();
                localCancelSource = null;
            }
        }

        public void Dispose()
        {
            for (var i = 0; i < this.targetStreams.Count; i++)
            {
                if (this.targetStreams[i] != null)
                {
                    this.targetStreams[i].Dispose();
                    this.targetStreams[i] = null;
                }
            }
            foreach (var item in this.installTaskConfigs)
                item.Dispose();
            this.installTaskConfigs.Clear();
        }
    }
}