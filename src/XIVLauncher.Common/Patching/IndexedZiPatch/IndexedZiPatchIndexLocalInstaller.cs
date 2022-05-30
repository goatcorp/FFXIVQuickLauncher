using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    public class IndexedZiPatchIndexLocalInstaller : IIndexedZiPatchIndexInstaller
    {
        private int cancellationTokenCounter = 1;
        private long lastProgressUpdateCounter = 0;
        private bool isDisposed = false;
        private IndexedZiPatchInstaller? instance;

        public event IndexedZiPatchInstaller.OnInstallProgressDelegate OnInstallProgress;
        public event IndexedZiPatchInstaller.OnVerifyProgressDelegate OnVerifyProgress;

        public IndexedZiPatchIndexLocalInstaller()
        {
            this.instance = null;
        }

        public void Dispose()
        {
            if (this.isDisposed)
                throw new ObjectDisposedException(GetType().FullName);

            this.isDisposed = true;
        }

        public Task ConstructFromPatchFile(IndexedZiPatchIndex patchIndex, int progressReportInterval = 250)
        {
            this.instance?.Dispose();
            this.instance = new(patchIndex)
            {
                ProgressReportInterval = progressReportInterval,
            };
            this.instance.OnInstallProgress += OnInstallProgress;
            this.instance.OnVerifyProgress += OnVerifyProgress;
            return Task.CompletedTask;
        }

        public async Task VerifyFiles(bool refine = false, int concurrentCount = 8, CancellationToken? cancellationToken = null)
        {
            await this.instance.VerifyFiles(refine, concurrentCount, cancellationToken);
        }

        public Task MarkFileAsMissing(int targetIndex, CancellationToken? cancellationToken = null)
        {
            this.instance.MarkFileAsMissing(targetIndex);
            return Task.CompletedTask;
        }

        public Task SetTargetStreamFromPathReadOnly(int targetIndex, string path, CancellationToken? cancellationToken = null)
        {
            this.instance.SetTargetStreamForRead(targetIndex, new FileStream(path, FileMode.Open, FileAccess.Read));
            return Task.CompletedTask;
        }

        public Task SetTargetStreamFromPathReadWrite(int targetIndex, string path, CancellationToken? cancellationToken = null)
        {
            this.instance.SetTargetStreamForWriteFromFile(targetIndex, new FileInfo(path));
            return Task.CompletedTask;
        }

        public Task SetTargetStreamsFromPathReadOnly(string rootPath, CancellationToken? cancellationToken = null)
        {
            this.instance.SetTargetStreamsFromPathReadOnly(rootPath);
            return Task.CompletedTask;
        }

        public Task SetTargetStreamsFromPathReadWriteForMissingFiles(string rootPath, CancellationToken? cancellationToken = null)
        {
            this.instance.SetTargetStreamsFromPathReadWriteForMissingFiles(rootPath);
            return Task.CompletedTask;
        }

        public async Task RepairNonPatchData(CancellationToken? cancellationToken = null)
        {
            await this.instance.RepairNonPatchData(cancellationToken);
        }

        public Task WriteVersionFiles(string rootPath, CancellationToken? cancellationToken = null)
        {
            this.instance.WriteVersionFiles(rootPath);
            return Task.CompletedTask;
        }

        public Task QueueInstall(int sourceIndex, Uri sourceUrl, string sid, int splitBy = 8, CancellationToken? cancellationToken = null)
        {
            this.instance.QueueInstall(sourceIndex, sourceUrl.OriginalString, sid, splitBy);
            return Task.CompletedTask;
        }

        public Task QueueInstall(int sourceIndex, FileInfo sourceFile, int splitBy = 8, CancellationToken? cancellationToken = null)
        {
            this.instance.QueueInstall(sourceIndex, sourceFile, splitBy);
            return Task.CompletedTask;
        }

        public async Task Install(int concurrentCount, CancellationToken? cancellationToken = null)
        {
            await this.instance.Install(concurrentCount, cancellationToken);
        }

        public Task<List<SortedSet<Tuple<int, int>>>> GetMissingPartIndicesPerPatch(CancellationToken? cancellationToken = null)
        {
            return Task.FromResult(this.instance.MissingPartIndicesPerPatch);
        }

        public Task<List<SortedSet<int>>> GetMissingPartIndicesPerTargetFile(CancellationToken? cancellationToken = null)
        {
            return Task.FromResult(this.instance.MissingPartIndicesPerTargetFile);
        }

        public Task<SortedSet<int>> GetSizeMismatchTargetFileIndices(CancellationToken? cancellationToken = null)
        {
            return Task.FromResult(this.instance.SizeMismatchTargetFileIndices);
        }

        public Task SetWorkerProcessPriority(ProcessPriorityClass subprocessPriority, CancellationToken? cancellationToken = null)
        {
            return Task.CompletedTask; // is a no-op locally
        }

        public Task MoveFile(string sourceFile, string targetFile, CancellationToken? cancellationToken = null)
        {
            var sourceParentDir = new DirectoryInfo(Path.GetDirectoryName(sourceFile));
            var targetParentDir = new DirectoryInfo(Path.GetDirectoryName(targetFile));

            targetParentDir.Create();
            new FileInfo(sourceFile).MoveTo(targetFile);

            if (!sourceParentDir.GetFileSystemInfos().Any())
                sourceParentDir.Delete(false);

            return Task.CompletedTask;
        }

        public Task CreateDirectory(string dir, CancellationToken? cancellationToken = null)
        {
            new DirectoryInfo(dir).Create();
            return Task.CompletedTask;
        }

        public Task RemoveDirectory(string dir, bool recursive = false, CancellationToken? cancellationToken = null)
        {
            new DirectoryInfo(dir).Delete(recursive);
            return Task.CompletedTask;
        }
    }
}