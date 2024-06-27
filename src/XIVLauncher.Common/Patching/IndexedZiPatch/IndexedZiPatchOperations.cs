using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.ZiPatch;

#nullable enable

namespace XIVLauncher.Common.Patching.IndexedZiPatch;

public class IndexedZiPatchOperations
{
    public static async Task<IndexedZiPatchIndex> CreateZiPatchIndices(int expacVersion, IList<string> patchFilePaths, CancellationToken cancellationToken = default)
    {
        var sources = new List<Stream>();
        var patchFiles = new List<ZiPatchFile>();
        var patchIndex = new IndexedZiPatchIndex(expacVersion);

        try
        {
            var firstPatchFileIndex = patchFilePaths.Count - 1;

            while (firstPatchFileIndex > 0)
            {
                if (File.Exists(patchFilePaths[firstPatchFileIndex] + ".index"))
                    break;

                firstPatchFileIndex--;
            }

            for (var i = 0; i < patchFilePaths.Count; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var patchFilePath = patchFilePaths[i];
                sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));
                patchFiles.Add(new(sources[sources.Count - 1]));

                if (i < firstPatchFileIndex)
                    continue;

                if (File.Exists(patchFilePath + ".index"))
                {
                    Log.Information("Reading patch index file {0}...", patchFilePath);
                    patchIndex = new(new BinaryReader(new DeflateStream(new FileStream(patchFilePath + ".index", FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));
                    continue;
                }

                Log.Information("Indexing patch file {0}...", patchFilePath);
                await patchIndex.ApplyZiPatch(Path.GetFileName(patchFilePath), patchFiles[patchFiles.Count - 1], cancellationToken);

                Log.Information("Calculating CRC32 for files resulted from patch file {0}...", patchFilePath);
                await patchIndex.CalculateCrc32(sources, cancellationToken);

                using (var writer = new BinaryWriter(new DeflateStream(new FileStream(patchFilePath + ".index.tmp", FileMode.Create), CompressionLevel.Optimal)))
                    patchIndex.WriteTo(writer);

                File.Move(patchFilePath + ".index.tmp", patchFilePath + ".index");
            }

            return patchIndex;
        }
        finally
        {
            foreach (var source in sources)
                source.Dispose();
        }
    }

    public static async Task<IndexedZiPatchInstaller> VerifyFromZiPatchIndex(string patchIndexFilePath, string gameRootPath, int concurrentCount, CancellationToken cancellationToken = default)
    {
        return await VerifyFromZiPatchIndex(
                   new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress))),
                   gameRootPath,
                   concurrentCount,
                   cancellationToken);
    }

    public static async Task<IndexedZiPatchInstaller> VerifyFromZiPatchIndex(IndexedZiPatchIndex patchIndex, string gameRootPath, int concurrentCount, CancellationToken cancellationToken = default)
    {
        var verifier = new IndexedZiPatchInstaller(patchIndex)
        {
            ProgressReportInterval = 1000
        };

        var remainingErrorMessagesToShow = 8;

        void OnVerifyProgressCallback(int index, long progress, long max) => Log.Information("[{0}/{1}] Checking file {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", index + 1, patchIndex.Length,
                                                                                             patchIndex[Math.Min(index, patchIndex.Length - 1)].RelativePath, progress / 1048576.0, max / 1048576.0,
                                                                                             100.0 * progress / max);

        void OnCorruptionFoundCallback(IndexedZiPatchPartLocator part, IndexedZiPatchPartLocator.VerifyDataResult result)
        {
            switch (result)
            {
                case IndexedZiPatchPartLocator.VerifyDataResult.FailNotEnoughData:
                    if (remainingErrorMessagesToShow > 0)
                    {
                        Log.Error("{0}:{1}:{2}: Premature EOF detected", patchIndex[part.TargetIndex].RelativePath, part.TargetOffset, patchIndex[part.TargetIndex].FileSize);
                        remainingErrorMessagesToShow = 0;
                    }

                    break;

                case IndexedZiPatchPartLocator.VerifyDataResult.FailBadData:
                    if (remainingErrorMessagesToShow > 0)
                    {
                        Log.Warning(
                            --remainingErrorMessagesToShow == 0 ? "{0}:{1}:{2}: Corrupt data; suppressing further corruption warnings for this file." : "{0}:{1}:{2}: Corrupt data",
                            patchIndex[part.TargetIndex].RelativePath, part.TargetOffset,
                            part.TargetEnd);
                    }

                    break;
            }
        }

        verifier.OnVerifyProgress += OnVerifyProgressCallback;
        verifier.OnCorruptionFound += OnCorruptionFoundCallback;

        try
        {
            verifier.SetTargetStreamsFromPathReadOnly(gameRootPath);
            await verifier.VerifyFiles(false, concurrentCount, cancellationToken);
        }
        finally
        {
            verifier.OnVerifyProgress -= OnVerifyProgressCallback;
            verifier.OnCorruptionFound -= OnCorruptionFoundCallback;
        }

        return verifier;
    }

    public static async Task RepairFromPatchFileIndexFromFile(
        IndexedZiPatchIndex patchIndex, string gameRootPath, string patchFileRootDir, int concurrentCount, CancellationToken cancellationToken = default)
    {
        using var verifier = await VerifyFromZiPatchIndex(patchIndex, gameRootPath, concurrentCount, cancellationToken);
        verifier.SetTargetStreamsFromPathReadWriteForMissingFiles(gameRootPath);
        for (var i = 0; i < patchIndex.Sources.Count; i++)
            verifier.QueueInstall(i, new(Path.Combine(patchFileRootDir, patchIndex.Sources[i])));
        await verifier.Install(concurrentCount, cancellationToken);
    }

    public static async Task RepairFromPatchFileIndexFromFile(
        string patchIndexFilePath, string gameRootPath, string patchFileRootDir, int concurrentCount, CancellationToken cancellationToken = default) =>
        await RepairFromPatchFileIndexFromFile(
            new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress))), gameRootPath,
            patchFileRootDir, concurrentCount, cancellationToken);

    public static async Task RepairFromPatchFileIndexFromUri(IndexedZiPatchIndex patchIndex, string gameRootPath, string baseUri, int concurrentCount, CancellationToken cancellationToken = default)
    {
        using var verifier = await VerifyFromZiPatchIndex(patchIndex, gameRootPath, concurrentCount, cancellationToken);
        verifier.SetTargetStreamsFromPathReadWriteForMissingFiles(gameRootPath);
        for (var i = 0; i < patchIndex.Sources.Count; i++)
            verifier.QueueInstall(i, baseUri + patchIndex.Sources[i], null, concurrentCount);

        void OnInstallProgressCallback(int index, long progress, long max, IndexedZiPatchInstaller.InstallTaskState state) => Log.Information(
            "[{0}/{1}] {2} {3}... {4:0.00}/{5:0.00}MB ({6:00.00}%)", index, patchIndex.Sources.Count, state, patchIndex.Sources[Math.Min(index, patchIndex.Sources.Count - 1)],
            progress / 1048576.0, max / 1048576.0, 100.0 * progress / max);

        verifier.OnInstallProgress += OnInstallProgressCallback;

        try
        {
            await verifier.Install(concurrentCount, cancellationToken);
            verifier.WriteVersionFiles(gameRootPath);
        }
        finally
        {
            verifier.OnInstallProgress -= OnInstallProgressCallback;
        }
    }

    public static async Task RepairFromPatchFileIndexFromUri(string patchIndexFilePath, string gameRootPath, string baseUri, int concurrentCount, CancellationToken cancellationToken = default) =>
        await RepairFromPatchFileIndexFromUri(
            new IndexedZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress))), gameRootPath, baseUri,
            concurrentCount, cancellationToken);

    private static async Task Test_Single(int expacVersion, string patchFilesPath, string rootPath, string baseUri, CancellationToken cancellationToken = default)
    {
        var patchFiles = Directory.GetFiles(Directory.GetDirectories(patchFilesPath).First(x => Path.GetFileName(x).Length == 8), "*.patch").ToList();
        patchFiles.Sort((x, y) => string.Compare(Path.GetFileName(x).Substring(1), Path.GetFileName(y).Substring(1), StringComparison.OrdinalIgnoreCase));
        var patchIndex = await CreateZiPatchIndices(expacVersion, patchFiles, cancellationToken);
        await RepairFromPatchFileIndexFromUri(patchIndex, rootPath, baseUri, 8, cancellationToken);
    }

    public static void Test()
    {
        CancellationTokenSource source = new();
        string[] patchFileBaseUrls =
        [
            "http://patch-dl.ffxiv.com/boot/2b5cbc63/"
        ];
        // source.Cancel();
        Task.WaitAll([
            Test_Single(IndexedZiPatchIndex.ExpacVersionBoot, @"Z:\patch-dl.ffxiv.com\boot", @"Z:\tgame\boot", patchFileBaseUrls[0], source.Token)
        ]);
    }
}
