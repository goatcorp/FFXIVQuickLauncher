using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
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
        private readonly List<Stream> TargetStreams = new();
        private readonly List<object> TargetLocks = new();

        public delegate void OnCorruptionFoundDelegate(IndexedZiPatchPartLocator part, IndexedZiPatchPartLocator.VerifyDataResult result);
        public delegate void OnVerifyProgressDelegate(int targetIndex, long progress, long max);
        public delegate void OnInstallProgressDelegate(int sourceIndex, long progress, long max);

        public event OnCorruptionFoundDelegate OnCorruptionFound;
        public event OnVerifyProgressDelegate OnVerifyProgress;
        public event OnInstallProgressDelegate OnInstallProgress;

        // Definitions taken from PInvoke.net (with some changes)
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

        public IndexedZiPatchInstaller(IndexedZiPatchIndex def)
        {
            Index = def;
            foreach (var _ in def.Targets)
            {
                MissingPartIndicesPerTargetFile.Add(new());
                TargetStreams.Add(null);
                TargetLocks.Add(new());
            }
            foreach (var _ in def.Sources)
                MissingPartIndicesPerPatch.Add(new());
        }

        public async Task VerifyFiles(int concurrentCount = 8, CancellationToken? cancellationToken = null)
        {
            CancellationTokenSource localCancelSource = new();

            if (cancellationToken.HasValue)
                cancellationToken.Value.Register(() => localCancelSource?.Cancel());

            List<Task> verifyTasks = new();
            try
            {
                long progressCounter = 0;
                long progressMax = Index.Targets.Select(x => x.FileSize).Sum();

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
                        var stream = TargetStreams[targetIndex];
                        if (stream == null)
                            continue;

                        var file = Index[targetIndex];
                        if (stream.Length != file.FileSize)
                            SizeMismatchTargetFileIndices.Add(targetIndex);

                        verifyTasks.Add(Task.Run(() =>
                        {
                            for (var j = 0; j < file.Count; ++j)
                            {
                                localCancelSource.Token.ThrowIfCancellationRequested();

                                var verifyResult = file[j].Verify(stream);
                                lock (verifyTasks)
                                {
                                    progressCounter += file[j].TargetSize;
                                    switch (verifyResult)
                                    {
                                        case IndexedZiPatchPartLocator.VerifyDataResult.Pass:
                                            break;

                                        case IndexedZiPatchPartLocator.VerifyDataResult.FailUnverifiable:
                                            throw new Exception($"{file.RelativePath}:{file[j].TargetOffset}:{file[j].TargetEnd}: Should not happen; unverifiable due to insufficient source data");

                                        case IndexedZiPatchPartLocator.VerifyDataResult.FailNotEnoughData:
                                        case IndexedZiPatchPartLocator.VerifyDataResult.FailBadData:
                                            if (file[j].IsFromSourceFile)
                                                MissingPartIndicesPerPatch[file[j].SourceIndex].Add(Tuple.Create(file[j].TargetIndex, j));
                                            MissingPartIndicesPerTargetFile[file[j].TargetIndex].Add(j);
                                            OnCorruptionFound?.Invoke(file[j], verifyResult);
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
            }
            finally
            {
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
                localCancelSource.Cancel();
                localCancelSource.Dispose();
                localCancelSource = null;
            }
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

        public void SetTargetStreamForRead(int targetIndex, Stream targetStream)
        {
            if (!targetStream.CanRead || !targetStream.CanSeek)
                throw new ArgumentException("Target stream must be readable and seekable.");

            TargetStreams[targetIndex] = targetStream;
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
                if (useSetFileValidData && !PInvoke.SetFileValidData(stream.SafeFileHandle.DangerousGetHandle(), file.FileSize))
                    Log.Information($"Unable to apply SetFileValidData on file {fileInfo.FullName} (error code {Marshal.GetLastWin32Error()})");

                using var buf = ReusableByteBufferManager.GetBuffer(65536);
                for (var i = 0; i < 65536; i++)
                    buf.Buffer[i] = 0xC0;
                stream.Seek(0, SeekOrigin.Begin);
                for (var i = 0; i < file.FileSize; i += 65536)
                    stream.Write(buf.Buffer, 0, (int) Math.Min(file.FileSize - i, 65536));
            }
            TargetStreams[targetIndex] = stream;
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

            var useSetFileValidData = false;
            try
            {
                PInvoke.SetCurrentPrivilege("SeManageVolumePrivilege", true);
            }
            catch (Win32Exception e)
            {
                Log.Information(e, "Unable to obtain SeManageVolumePrivilege; not using SetFileValidData.");
                useSetFileValidData = false;
            }

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
            var target = TargetStreams[targetIndex];
            if (target == null)
                return;

            lock (TargetLocks[targetIndex])
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
                for (int i = 0, i_ = Index.Length; i < i_; i++)
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

            public InstallTaskConfig(IndexedZiPatchInstaller installer, int sourceIndex, IEnumerable<Tuple<int, int>> targetPartIndices)
            {
                Index = installer.Index;
                Installer = installer;
                SourceIndex = sourceIndex;
                TargetPartIndices = targetPartIndices.ToList();
            }

            public virtual void Again(IEnumerable<Tuple<int, int>> targetPartIndices)
            {
                TargetPartIndices.Clear();
                TargetPartIndices.AddRange(targetPartIndices);
            }

            public abstract bool ShouldReattempt { get; }

            public abstract Task Repair(CancellationToken? cancellationToken = null);

            public virtual void Dispose() { }
        }

        public class HttpInstallTaskConfig : InstallTaskConfig
        {
            public readonly HttpClient Client;
            public readonly string SourceUrl;
            private readonly List<long> TargetPartOffsets;
            private readonly string Sid;
            private int AttemptIndex = 0;

            public HttpInstallTaskConfig(IndexedZiPatchInstaller installer, int sourceIndex, IEnumerable<Tuple<int, int>> targetPartIndices, HttpClient client, string sourceUrl, string sid)
                : base(installer, sourceIndex, targetPartIndices)
            {
                Client = client;
                SourceUrl = sourceUrl;
                Sid = sid;
                TargetPartIndices.Sort((x, y) => Index[x.Item1][x.Item2].SourceOffset.CompareTo(Index[y.Item1][y.Item2].SourceOffset));
                TargetPartOffsets = TargetPartIndices.Select(x => Index[x.Item1][x.Item2].SourceOffset).ToList();

                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    ProgressMax += Index[targetIndex][partIndex].TargetSize;
            }

            public override void Again(IEnumerable<Tuple<int, int>> targetPartIndices)
            {
                base.Again(targetPartIndices);
                TargetPartIndices.Sort((x, y) => Index[x.Item1][x.Item2].SourceOffset.CompareTo(Index[y.Item1][y.Item2].SourceOffset));
                TargetPartOffsets.Clear();
                TargetPartOffsets.AddRange(TargetPartIndices.Select(x => Index[x.Item1][x.Item2].SourceOffset));
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    ProgressMax += Index[targetIndex][partIndex].TargetSize;
            }

            public override bool ShouldReattempt => AttemptIndex < 8;

            public override async Task Repair(CancellationToken? cancellationToken = null)
            {
                if (AttemptIndex >= 2)
                {
                    // Exponential backoff
                    if (cancellationToken.HasValue)
                        await Task.Delay(1000 * (1 << Math.Min(5, AttemptIndex - 2)), cancellationToken.Value);
                    else
                        await Task.Delay(1000 * (1 << Math.Min(5, AttemptIndex - 2)));
                }
                AttemptIndex++;
                var offsets = new List<Tuple<long, long>>();
                offsets.Clear();
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    offsets.Add(Tuple.Create(Index[targetIndex][partIndex].SourceOffset, Math.Min(Index.GetSourceLastPtr(SourceIndex), Index[targetIndex][partIndex].MaxSourceEnd)));
                offsets.Sort();

                if (!offsets.Any())
                    return;

                for (int i = 1; i < offsets.Count;)
                {
                    if (offsets[i].Item1 - offsets[i - 1].Item2 < 1024)
                    {
                        offsets[i - 1] = Tuple.Create(offsets[i - 1].Item1, Math.Max(offsets[i - 1].Item2, offsets[i].Item2));
                        offsets.RemoveAt(i);
                    }
                    else
                        i += 1;
                }

                for (int i = 0; i < offsets.Count; i += 1024)
                {
                    using HttpRequestMessage req = new(HttpMethod.Get, SourceUrl);
                    req.Headers.Add("Range", "bytes=" + string.Join(",", offsets.Skip(i).Take(Math.Min(offsets.Count - i, 1024)).Select(x => $"{x.Item1}-{x.Item2 - 1}")));
                    if (Sid != null)
                        req.Headers.Add("X-Patch-Unique-Id", Sid);
                    req.Headers.Add("User-Agent", Constants.PatcherUserAgent);
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

                        while (fromTupleIndex < TargetPartOffsets.Count && TargetPartOffsets[fromTupleIndex] < n.AvailableToOffset)
                        {
                            if (cancellationToken.HasValue)
                                cancellationToken.Value.ThrowIfCancellationRequested();

                            var (targetIndex, partIndex) = TargetPartIndices[fromTupleIndex];
                            var part = Index[targetIndex][partIndex];

                            using var buffer = ReusableByteBufferManager.GetBuffer(part.TargetSize);
                            part.Reconstruct(n, buffer.Buffer);
                            Installer.WriteToTarget(part.TargetIndex, part.TargetOffset, buffer.Buffer, 0, (int)part.TargetSize);

                            ProgressValue += part.TargetSize;
                            CompletedTargetPartIndices.Add(TargetPartIndices[fromTupleIndex]);
                            TargetPartIndices.RemoveAt(fromTupleIndex);
                            TargetPartOffsets.RemoveAt(fromTupleIndex);
                        }
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

            public StreamInstallTaskConfig(IndexedZiPatchInstaller installer, int sourceIndex, IEnumerable<Tuple<int, int>> targetPartIndices, Stream sourceStream)
                : base(installer, sourceIndex, targetPartIndices)
            {
                SourceStream = sourceStream;
                long totalTargetSize = 0;
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    totalTargetSize += Index[targetIndex][partIndex].TargetSize;
                ProgressMax = totalTargetSize;
            }

            public override void Again(IEnumerable<Tuple<int, int>> targetPartIndices)
            {
                base.Again(targetPartIndices);
                TargetPartIndices.Sort((x, y) => Index[x.Item1][x.Item2].SourceOffset.CompareTo(Index[y.Item1][y.Item2].SourceOffset));
                foreach (var (targetIndex, partIndex) in TargetPartIndices)
                    ProgressMax += Index[targetIndex][partIndex].TargetSize;
            }

            public override bool ShouldReattempt => false;

            public override async Task Repair(CancellationToken? cancellationToken)
            {
                await Task.Run(() =>
                {
                    while (TargetPartIndices.Any())
                    {
                        if (cancellationToken.HasValue)
                            cancellationToken.Value.ThrowIfCancellationRequested();

                        var (targetIndex, partIndex) = TargetPartIndices[0];
                        var part = Index[targetIndex][partIndex];

                        using var buffer = ReusableByteBufferManager.GetBuffer(part.TargetSize);
                        part.Reconstruct(SourceStream, buffer.Buffer);
                        Installer.WriteToTarget(part.TargetIndex, part.TargetOffset, buffer.Buffer, 0, (int)part.TargetSize);

                        ProgressValue += part.TargetSize;
                        CompletedTargetPartIndices.Add(TargetPartIndices[0]);
                        TargetPartIndices.RemoveAt(0);
                    }
                    if (TargetPartIndices.Any())
                        throw new IOException("Missing target part remains");
                });
            }

            public override void Dispose()
            {
                SourceStream.Dispose();
                base.Dispose();
            }
        }

        private readonly List<InstallTaskConfig> InstallTaskConfigs = new();

        public void QueueInstall(int sourceIndex, HttpClient client, string sourceUrl, string sid, ISet<Tuple<int, int>> targetPartIndices)
        {
            if (targetPartIndices.Any())
                InstallTaskConfigs.Add(new HttpInstallTaskConfig(this, sourceIndex, targetPartIndices, client, sourceUrl, sid == "" ? null : sid));
        }

        public void QueueInstall(int sourceIndex, HttpClient client, string sourceUrl, string sid, int splitBy = 8)
        {
            var indices = MissingPartIndicesPerPatch[sourceIndex];
            var indicesPerRequest = (int)Math.Ceiling(1.0 * indices.Count / splitBy);
            for (int j = 0; j < indices.Count; j += indicesPerRequest)
                QueueInstall(sourceIndex, client, sourceUrl, sid, indices.Skip(j).Take(Math.Min(indicesPerRequest, indices.Count - j)).ToHashSet());
        }

        public void QueueInstall(int sourceIndex, Stream stream, ISet<Tuple<int, int>> targetPartIndices)
        {
            if (targetPartIndices.Any())
                InstallTaskConfigs.Add(new StreamInstallTaskConfig(this, sourceIndex, targetPartIndices, stream));
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
                QueueInstall(sourceIndex, file, indices.Skip(j).Take(Math.Min(indicesPerRequest, indices.Count - j)).ToHashSet());
        }

        public async Task Install(int concurrentCount, CancellationToken? cancellationToken = null)
        {
            if (!InstallTaskConfigs.Any())
            {
                await RepairNonPatchData();
                return;
            }

            long progressMax = InstallTaskConfigs.Select(x => x.ProgressMax).Sum();

            Task progressReportTask = null;
            Queue<InstallTaskConfig> pendingTaskConfigs = new();
            foreach (var x in InstallTaskConfigs)
                pendingTaskConfigs.Enqueue(x);

            Dictionary<Task, InstallTaskConfig> runningTasks = new();

            var exceptions = new List<Exception>();

            while (pendingTaskConfigs.Any() || runningTasks.Any())
            {
                if (cancellationToken.HasValue)
                    cancellationToken.Value.ThrowIfCancellationRequested();

                while (pendingTaskConfigs.Any() && runningTasks.Count < concurrentCount)
                {
                    var config = pendingTaskConfigs.Dequeue();
                    runningTasks[config.Repair(cancellationToken)] = config;
                }

                var taskIndex = Math.Max(0, InstallTaskConfigs.Count - pendingTaskConfigs.Count - runningTasks.Count - 1);
                var sourceIndexForProgressDisplay = InstallTaskConfigs[Math.Min(taskIndex, InstallTaskConfigs.Count - 1)].SourceIndex;
                OnInstallProgress?.Invoke(sourceIndexForProgressDisplay, InstallTaskConfigs.Select(x => x.ProgressValue).Sum(), progressMax);

                if (progressReportTask == null || progressReportTask.IsCompleted)
                    progressReportTask = cancellationToken.HasValue ? Task.Delay(ProgressReportInterval, cancellationToken.Value) : Task.Delay(ProgressReportInterval);
                runningTasks[progressReportTask] = null;
                await Task.WhenAny(runningTasks.Keys);
                foreach (var kvp in runningTasks)
                {
                    if (kvp.Value == null || !kvp.Key.IsCompleted)
                        continue;

                    if (kvp.Key.IsFaulted)
                    {
                        if (kvp.Value.ShouldReattempt)
                        {
                            Log.Warning(kvp.Key.Exception, "Exception occurred while loading part; trying again");
                            pendingTaskConfigs.Enqueue(kvp.Value);
                        }
                        else
                            exceptions.Add(kvp.Key.Exception);
                    }
                    else if (!kvp.Key.IsCanceled)
                    {
                        List<Tuple<int, int>> again = new();
                        foreach (var (targetIndex, partIndex) in kvp.Value.CompletedTargetPartIndices)
                        {
                            var part = Index[targetIndex][partIndex];
                            lock (TargetLocks[targetIndex])
                            {
                                if (part.Verify(TargetStreams[targetIndex]) == IndexedZiPatchPartLocator.VerifyDataResult.Pass)
                                    continue;

                                Log.Warning($"Bad part: source={kvp.Value.SourceIndex}:{part.SourceOffset}:{part.MaxSourceSize}, target={part.TargetIndex}:{partIndex}:{part.TargetOffset}:{part.TargetSize}");
                                again.Add(Tuple.Create(targetIndex, partIndex));
                            }
                        }
                        if (again.Any())
                        {
                            if (kvp.Value.ShouldReattempt)
                            {
                                Log.Warning("Some of downloaded parts came out wrong; trying again");
                                kvp.Value.Again(again);
                                pendingTaskConfigs.Enqueue(kvp.Value);
                            }
                            else
                                throw new IOException("Some of downloaded parts came out wrong");
                        }
                    }
                }
                runningTasks.Keys.Where(p => p.IsCompleted || p.IsCanceled || p.IsFaulted || p == progressReportTask).ToList().ForEach(p => runningTasks.Remove(p));
            }
            await RepairNonPatchData();

            if (exceptions.Count == 1)
                throw exceptions[0];
            else if (exceptions.Count > 1)
                throw new AggregateException("More than one error has occurred while installing.", exceptions);
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