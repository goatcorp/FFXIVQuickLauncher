using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace XIVLauncher.Common.Patching.IndexedZiPatch;

/// <summary>Common functions for ZiPatch index installers.</summary>
public interface IIndexedZiPatchIndexInstaller : IDisposable
{
    /// <summary>Invoked whenever install progress changes, with rate throttling specified from <see cref="ConstructFromPatchFile"/>.</summary>
    event IndexedZiPatchInstaller.OnInstallProgressDelegate? OnInstallProgress;

    /// <summary>Invoked whenever verify progress changes, with rate throttling specified from <see cref="ConstructFromPatchFile"/>.</summary>
    event IndexedZiPatchInstaller.OnVerifyProgressDelegate? OnVerifyProgress;

    /// <summary>Initializes the installer from the given patch file.</summary>
    /// <param name="patchIndex">Patch index file to load.</param>
    /// <param name="progressReportInterval">Rate of <see cref="OnInstallProgress"/> and <see cref="OnVerifyProgress"/> being called. If default, then 250ms will be used.</param>
    /// <returns>A task representing the operation state.</returns>
    Task ConstructFromPatchFile(IndexedZiPatchIndex patchIndex, TimeSpan progressReportInterval = default);

    /// <summary>Verifies all files that can be verified from the currently loaded patch index file.</summary>
    /// <param name="refine">If set, only verify the parts that are registered as missing part indices per target file.</param>
    /// <param name="concurrentCount">Number of threads for concurrent operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task VerifyFiles(bool refine = false, int concurrentCount = 8, CancellationToken cancellationToken = default);

    /// <summary>Marks a file as missing.</summary>
    /// <param name="targetIndex">Index of the target file in the patch index file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task MarkFileAsMissing(int targetIndex, CancellationToken cancellationToken = default);

    /// <summary>Opens a file specified for reading and use that stream as the file corresponding to the target index.</summary>
    /// <param name="targetIndex">Index of the target file in the patch index file.</param>
    /// <param name="path">Path of the file on the local file system.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task SetTargetStreamFromPathReadOnly(int targetIndex, string path, CancellationToken cancellationToken = default);

    /// <summary>Opens a file specified for reading and writing and use that stream as the file corresponding to the target index.</summary>
    /// <param name="targetIndex">Index of the target file in the patch index file.</param>
    /// <param name="path">Path of the file on the local file system.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task SetTargetStreamFromPathReadWrite(int targetIndex, string path, CancellationToken cancellationToken = default);

    /// <summary>Opens all files inside a root directory that are specified from the patch index file for reading.</summary>
    /// <param name="rootPath">Root path to find the files specified from the patch index file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task SetTargetStreamsFromPathReadOnly(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>Opens all files inside a root directory that are specified from the patch index file for reading and writing.</summary>
    /// <param name="rootPath">Root path to find the files specified from the patch index file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task SetTargetStreamsFromPathReadWriteForMissingFiles(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>Writes files that are not directly stored in patch files, such as blocks of zeroes.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task RepairNonPatchData(CancellationToken cancellationToken = default);

    /// <summary>Writes version files.</summary>
    /// <param name="rootPath">Path of the directory to write version files to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task WriteVersionFiles(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>Queues installation from a patch file.</summary>
    /// <param name="sourceIndex">Index of the patch file.</param>
    /// <param name="sourceUrl">URL of the patch file.</param>
    /// <param name="sid">Session ID to use to authenticate against file servers.</param>
    /// <param name="splitBy">Number of chunks to divide the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task QueueInstall(int sourceIndex, Uri sourceUrl, string? sid, int splitBy = 8, CancellationToken cancellationToken = default);

    /// <summary>Queues installation from a patch file.</summary>
    /// <param name="sourceIndex">Index of the patch file.</param>
    /// <param name="sourceFile">Local path of the patch file.</param>
    /// <param name="splitBy">Number of chunks to divide the processing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task QueueInstall(int sourceIndex, FileInfo sourceFile, int splitBy = 8, CancellationToken cancellationToken = default);

    /// <summary>Installs from the queued patch files.</summary>
    /// <param name="concurrentCount">Number of concurrent operations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task Install(int concurrentCount, CancellationToken cancellationToken = default);

    /// <summary>Gets the missing part indices per patch file.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task<List<SortedSet<Tuple<int, int>>>> GetMissingPartIndicesPerPatch(CancellationToken cancellationToken = default);

    /// <summary>Gets the missing part indices per target file.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task<List<SortedSet<int>>> GetMissingPartIndicesPerTargetFile(CancellationToken cancellationToken = default);

    /// <summary>Gets the target file indices for the files with incorrect file sizes.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task<SortedSet<int>> GetSizeMismatchTargetFileIndices(CancellationToken cancellationToken = default);

    /// <summary>Sets worker process' priority, if applicable.</summary>
    /// <param name="subprocessPriority">Desired process priority.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task SetWorkerProcessPriority(ProcessPriorityClass subprocessPriority, CancellationToken cancellationToken = default);

    /// <summary>Moves a file using the worker process' permissions.</summary>
    /// <param name="sourceFile">Path of the source file.</param>
    /// <param name="targetFile">New path to move the source file to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task MoveFile(string sourceFile, string targetFile, CancellationToken cancellationToken = default);

    /// <summary>Creates a directory using the worker process' permissions.</summary>
    /// <param name="dir">Directory to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task CreateDirectory(string dir, CancellationToken cancellationToken = default);

    /// <summary>Removes a directory using the worker process' permissions.</summary>
    /// <param name="dir">Directory to remove.</param>
    /// <param name="recursive">Whether to remove the directory recursively.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation state.</returns>
    Task RemoveDirectory(string dir, bool recursive = false, CancellationToken cancellationToken = default);
}
