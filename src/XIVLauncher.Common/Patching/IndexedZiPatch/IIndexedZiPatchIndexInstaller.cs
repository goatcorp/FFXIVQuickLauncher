using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    public interface IIndexedZiPatchIndexInstaller : IDisposable
    {
        public event IndexedZiPatchInstaller.OnInstallProgressDelegate OnInstallProgress;
        public event IndexedZiPatchInstaller.OnVerifyProgressDelegate OnVerifyProgress;

        public Task ConstructFromPatchFile(IndexedZiPatchIndex patchIndex, int progressReportInterval = 250);

        public Task VerifyFiles(bool refine = false, int concurrentCount = 8, CancellationToken? cancellationToken = null);

        public Task MarkFileAsMissing(int targetIndex, CancellationToken? cancellationToken = null);

        public Task SetTargetStreamFromPathReadOnly(int targetIndex, string path, CancellationToken? cancellationToken = null);

        public Task SetTargetStreamFromPathReadWrite(int targetIndex, string path, CancellationToken? cancellationToken = null);

        public Task SetTargetStreamsFromPathReadOnly(string rootPath, CancellationToken? cancellationToken = null);

        public Task SetTargetStreamsFromPathReadWriteForMissingFiles(string rootPath, CancellationToken? cancellationToken = null);

        public Task RepairNonPatchData(CancellationToken? cancellationToken = null);

        public Task WriteVersionFiles(string rootPath, CancellationToken? cancellationToken = null);

        public Task QueueInstall(int sourceIndex, Uri sourceUrl, string sid, int splitBy = 8, CancellationToken? cancellationToken = null);

        public Task QueueInstall(int sourceIndex, FileInfo sourceFile, int splitBy = 8, CancellationToken? cancellationToken = null);

        public Task Install(int concurrentCount, CancellationToken? cancellationToken = null);

        public Task<List<SortedSet<Tuple<int, int>>>> GetMissingPartIndicesPerPatch(CancellationToken? cancellationToken = null);

        public Task<List<SortedSet<int>>> GetMissingPartIndicesPerTargetFile(CancellationToken? cancellationToken = null);

        public Task<SortedSet<int>> GetSizeMismatchTargetFileIndices(CancellationToken? cancellationToken = null);

        public Task SetWorkerProcessPriority(ProcessPriorityClass subprocessPriority, CancellationToken? cancellationToken = null);

        public Task MoveFile(string sourceFile, string targetFile, CancellationToken? cancellationToken = null);

        public Task CreateDirectory(string dir, CancellationToken? cancellationToken = null);

        public Task RemoveDirectory(string dir, bool recursive = false, CancellationToken? cancellationToken = null);
    }
}