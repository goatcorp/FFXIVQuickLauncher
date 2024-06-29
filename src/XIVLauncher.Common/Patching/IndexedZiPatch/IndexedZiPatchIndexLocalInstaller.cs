using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace XIVLauncher.Common.Patching.IndexedZiPatch;

public class IndexedZiPatchIndexLocalInstaller : IIndexedZiPatchIndexInstaller
{
    private bool isDisposed;
    private IndexedZiPatchInstaller? instance;

    public event IndexedZiPatchInstaller.OnInstallProgressDelegate? OnInstallProgress;
    public event IndexedZiPatchInstaller.OnVerifyProgressDelegate? OnVerifyProgress;

    public void Dispose()
    {
        if (this.isDisposed)
            throw new ObjectDisposedException(GetType().FullName);

        this.isDisposed = true;
    }

    public Task ConstructFromPatchFile(IndexedZiPatchIndex patchIndex, TimeSpan progressReportInterval = default)
    {
        this.instance?.Dispose();
        this.instance = new(patchIndex)
        {
            ProgressReportInterval = progressReportInterval.TotalMilliseconds > 0 ? (int)progressReportInterval.TotalMilliseconds : 250,
        };
        this.instance.OnInstallProgress += OnInstallProgress;
        this.instance.OnVerifyProgress += OnVerifyProgress;
        return Task.CompletedTask;
    }

    public async Task VerifyFiles(bool refine = false, int concurrentCount = 8, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        await this.instance.VerifyFiles(refine, concurrentCount, cancellationToken);
    }

    public Task MarkFileAsMissing(int targetIndex, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.MarkFileAsMissing(targetIndex);
        return Task.CompletedTask;
    }

    public Task SetTargetStreamFromPathReadOnly(int targetIndex, string path, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.SetTargetStreamForRead(targetIndex, new FileStream(path, FileMode.Open, FileAccess.Read));
        return Task.CompletedTask;
    }

    public Task SetTargetStreamFromPathReadWrite(int targetIndex, string path, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.SetTargetStreamForWriteFromFile(targetIndex, new(path));
        return Task.CompletedTask;
    }

    public Task SetTargetStreamsFromPathReadOnly(string rootPath, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.SetTargetStreamsFromPathReadOnly(rootPath);
        return Task.CompletedTask;
    }

    public Task SetTargetStreamsFromPathReadWriteForMissingFiles(string rootPath, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.SetTargetStreamsFromPathReadWriteForMissingFiles(rootPath);
        return Task.CompletedTask;
    }

    public async Task RepairNonPatchData(CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        await this.instance.RepairNonPatchData(cancellationToken);
    }

    public Task WriteVersionFiles(string rootPath, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.WriteVersionFiles(rootPath);
        return Task.CompletedTask;
    }

    public Task QueueInstall(int sourceIndex, Uri sourceUrl, string? sid, int splitBy = 8, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.QueueInstall(sourceIndex, sourceUrl.OriginalString, sid, splitBy);
        return Task.CompletedTask;
    }

    public Task QueueInstall(int sourceIndex, FileInfo sourceFile, int splitBy = 8, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        this.instance.QueueInstall(sourceIndex, sourceFile, splitBy);
        return Task.CompletedTask;
    }

    public async Task Install(int concurrentCount, CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        await this.instance.Install(concurrentCount, cancellationToken);
    }

    public Task<List<SortedSet<Tuple<int, int>>>> GetMissingPartIndicesPerPatch(CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        return Task.FromResult(this.instance.MissingPartIndicesPerPatch);
    }

    public Task<List<SortedSet<int>>> GetMissingPartIndicesPerTargetFile(CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        return Task.FromResult(this.instance.MissingPartIndicesPerTargetFile);
    }

    public Task<SortedSet<int>> GetSizeMismatchTargetFileIndices(CancellationToken cancellationToken = default)
    {
        if (this.instance is null)
            throw new InvalidOperationException("Installer is not initialized.");

        return Task.FromResult(this.instance.SizeMismatchTargetFileIndices);
    }

    public Task SetWorkerProcessPriority(ProcessPriorityClass subprocessPriority, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask; // is a no-op locally
    }

    public Task MoveFile(string sourceFile, string targetFile, CancellationToken cancellationToken = default)
    {
        var sourceParentDir = new DirectoryInfo(Path.GetDirectoryName(sourceFile) ?? throw new InvalidOperationException());
        var targetParentDir = new DirectoryInfo(Path.GetDirectoryName(targetFile.EndsWith("/", StringComparison.Ordinal) ? targetFile.Substring(0, targetFile.Length - 1) : targetFile) ?? throw new InvalidOperationException());
        targetParentDir.Create();
        Directory.Move(sourceFile, targetFile);

        if (!sourceParentDir.GetFileSystemInfos().Any())
            sourceParentDir.Delete(false);

        return Task.CompletedTask;
    }

    public Task CreateDirectory(string dir, CancellationToken cancellationToken = default)
    {
        new DirectoryInfo(dir).Create();
        return Task.CompletedTask;
    }

    public Task RemoveDirectory(string dir, bool recursive = false, CancellationToken cancellationToken = default)
    {
        new DirectoryInfo(dir).Delete(recursive);
        return Task.CompletedTask;
    }
}
