using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.ZiPatch;

namespace XIVLauncher.PatchInstaller.IndexedPatch
{
    public class PartialPatchOperations
    {
        public static async Task<ZiPatchIndex> CreateZiPatchIndices(int expacVersion, IList<string> patchFilePaths, CancellationToken? cancellationToken = null)
        {
            var sources = new List<Stream>();
            var patchFiles = new List<ZiPatchFile>();
            var patchIndex = new ZiPatchIndex(expacVersion);
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
                    if (cancellationToken.HasValue)
                        cancellationToken.Value.ThrowIfCancellationRequested();

                    var patchFilePath = patchFilePaths[i];
                    sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));
                    patchFiles.Add(new ZiPatchFile(sources[sources.Count - 1]));

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

        public static async Task<ZiPatchIndexInstaller> VerifyFromZiPatchIndex(string patchIndexFilePath, string gameRootPath, CancellationToken? cancellationToken = null) => await VerifyFromZiPatchIndex(new ZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress))), gameRootPath, cancellationToken);

        public static async Task<ZiPatchIndexInstaller> VerifyFromZiPatchIndex(ZiPatchIndex patchIndex, string gameRootPath, CancellationToken? cancellationToken = null)
        {
            using var verifier = new ZiPatchIndexInstaller(patchIndex)
            {
                ProgressReportInterval = 1000
            };

            verifier.OnProgress.Clear();
            verifier.OnProgress.Add((PartialFilePart part, long progress, long max) => Log.Information("[{0}/{1}] Checking file {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", part.TargetIndex + 1, patchIndex.Length, patchIndex[part.TargetIndex].RelativePath, progress / 1048576.0, max / 1048576.0, 100.0 * progress / max));

            var remainingErrorMessagesToShow = 8;
            verifier.OnCorruptionFound.Clear();
            verifier.OnCorruptionFound.Add((PartialFilePart part, PartialFilePart.VerifyDataResult result) =>
            {
                switch (result)
                {
                    case PartialFilePart.VerifyDataResult.FailNotEnoughData:
                        if (remainingErrorMessagesToShow > 0)
                        {
                            Log.Error("{0}:{1}:{2}: Premature EOF detected", patchIndex[part.TargetIndex].RelativePath, part.TargetOffset, patchIndex[part.TargetIndex].FileSize);
                            remainingErrorMessagesToShow = 0;
                        }
                        break;

                    case PartialFilePart.VerifyDataResult.FailBadData:
                        if (remainingErrorMessagesToShow > 0)
                        {
                            if (--remainingErrorMessagesToShow == 0)
                                Log.Warning("{0}:{1}:{2}: Corrupt data; suppressing further corruption warnings for this file.", patchIndex[part.TargetIndex].RelativePath, part.TargetOffset, part.TargetEnd);
                            else
                                Log.Warning("{0}:{1}:{2}: Corrupt data", patchIndex[part.TargetIndex].RelativePath, part.TargetOffset, part.TargetEnd);
                        }
                        break;
                }
            });

            verifier.SetTargetStreamsFromPathReadOnly(gameRootPath);
            await verifier.VerifyFiles(cancellationToken);

            return verifier;
        }

        public static async Task RepairFromPatchFileIndexFromFile(ZiPatchIndex patchIndex, string gameRootPath, string patchFileRootDir, int concurrentCount, CancellationToken? cancellationToken = null)
        {
            using var verifier = await VerifyFromZiPatchIndex(patchIndex, gameRootPath, cancellationToken);
            verifier.SetTargetStreamsFromPathWriteMissingOnly(gameRootPath);
            for (var i = 0; i < patchIndex.Sources.Count; i++)
                verifier.QueueInstall(i, new FileStream(Path.Combine(patchFileRootDir, patchIndex.Sources[i]), FileMode.Open, FileAccess.Read));
            await verifier.Install(concurrentCount, cancellationToken);
        }

        public static async Task RepairFromPatchFileIndexFromFile(string patchIndexFilePath, string gameRootPath, string patchFileRootDir, int concurrentCount, CancellationToken? cancellationToken = null) => await RepairFromPatchFileIndexFromFile(new ZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress))), gameRootPath, patchFileRootDir, concurrentCount, cancellationToken);

        public static async Task RepairFromPatchFileIndexFromUri(ZiPatchIndex patchIndex, string gameRootPath, HttpClient client, string baseUri, int concurrentCount, CancellationToken? cancellationToken = null)
        {
            using var verifier = await VerifyFromZiPatchIndex(patchIndex, gameRootPath, cancellationToken);
            verifier.SetTargetStreamsFromPathWriteMissingOnly(gameRootPath);
            for (var i = 0; i < patchIndex.Sources.Count; i++)
                verifier.QueueInstall(i, client, baseUri + patchIndex.Sources[i], concurrentCount);

            verifier.OnProgress.Clear();
            verifier.OnProgress.Add((PartialFilePart part, long progress, long max) => Log.Information("[{0}/{1}] Installing {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", part.SourceIndex + 1, patchIndex.Sources.Count, patchIndex.Sources[part.SourceIndex], progress / 1048576.0, max / 1048576.0, 100.0 * progress / max));

            await verifier.Install(concurrentCount, cancellationToken);
            verifier.WriteVersionFiles(gameRootPath);
        }

        public static async Task RepairFromPatchFileIndexFromUri(string patchIndexFilePath, string gameRootPath, HttpClient client, string baseUri, int concurrentCount, CancellationToken? cancellationToken = null) => await RepairFromPatchFileIndexFromUri(new ZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress))), gameRootPath, client, baseUri, concurrentCount, cancellationToken);

        public static async Task ExampleRepair(CancellationToken? cancellationToken = null)
        {
            var gameRootPath = @"Z:\tgame\boot";
            var patchIndexFilePath = "boot.index";
            var availableSourceUrls = new Dictionary<string, string>() {
                {"D2013.06.18.0000.0000.patch", "http://example.com/D2013.06.18.0000.0000.patch" },
                {"D2021.11.16.0000.0001.patch", "http://example.com/D2013.06.18.0000.0000.patch" },
            };
            var maxConcurrentConnectionsForPatchSet = 8;

            var patchIndex = new ZiPatchIndex(new BinaryReader(new DeflateStream(new FileStream(patchIndexFilePath, FileMode.Open, FileAccess.Read), CompressionMode.Decompress)));
            using var verifier = new ZiPatchIndexInstaller(patchIndex) { ProgressReportInterval = 1000 };
            using var client = new HttpClient();

            verifier.OnProgress.Add((PartialFilePart part, long progress, long max) => Log.Information("[{0}/{1}] Checking file {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", part.TargetIndex + 1, patchIndex.Length, patchIndex[part.TargetIndex].RelativePath, progress / 1048576.0, max / 1048576.0, 100.0 * progress / max));
            verifier.SetTargetStreamsFromPathReadOnly(gameRootPath);
            await verifier.VerifyFiles(cancellationToken);
            verifier.OnProgress.Clear();

            verifier.OnProgress.Add((PartialFilePart part, long progress, long max) => Log.Information("[{0}/{1}] Installing {2}... {3:0.00}/{4:0.00}MB ({5:00.00}%)", part.SourceIndex + 1, patchIndex.Sources.Count, patchIndex.Sources[part.SourceIndex], progress / 1048576.0, max / 1048576.0, 100.0 * progress / max));
            verifier.SetTargetStreamsFromPathWriteMissingOnly(gameRootPath);
            foreach (var source in availableSourceUrls)
                verifier.QueueInstall(patchIndex.Sources.IndexOf(source.Key), client, source.Value, maxConcurrentConnectionsForPatchSet);
            await verifier.Install(maxConcurrentConnectionsForPatchSet, cancellationToken);
            verifier.WriteVersionFiles(gameRootPath);
        }

        private static async Task Test_Single(int expacVersion, string patchFilesPath, string rootPath, HttpClient client, string baseUri, CancellationToken? cancellationToken = null)
        {
            var patchFiles = Directory.GetFiles(Directory.GetDirectories(patchFilesPath).Where(x => Path.GetFileName(x).Length == 8).First(), "*.patch").ToList();
            patchFiles.Sort((x, y) => Path.GetFileName(x).Substring(1).CompareTo(Path.GetFileName(y).Substring(1)));
            var patchIndex = await CreateZiPatchIndices(expacVersion, patchFiles, cancellationToken);
            await RepairFromPatchFileIndexFromUri(patchIndex, rootPath, client, baseUri, 8, cancellationToken);
        }

        public static void Test()
        {
            using HttpClient client = new();
            CancellationTokenSource source = new();
            string[] patchFileBaseUrls = new string[] {
                // TODO: replace these with dummy URLs on commits
                "http://example.com/boot/",
                "http://example.com/game/",
                "http://example.com/game/ex1/",
                "http://example.com/game/ex2/",
                "http://example.com/game/ex3/",
                "http://example.com/game/ex4/",
            };
            // source.Cancel();
            Task.WaitAll(new Task[] {
                Test_Single(ZiPatchIndex.EXPAC_VERSION_BOOT, @"Z:\patch-dl.ffxiv.com\boot", @"Z:\tgame\boot", client, patchFileBaseUrls[0], source.Token),
                Test_Single(ZiPatchIndex.EXPAC_VERSION_BASE_GAME, @"Z:\patch-dl.ffxiv.com\game", @"Z:\tgame\game", client, patchFileBaseUrls[1], source.Token),
                Test_Single(1, @"Z:\patch-dl.ffxiv.com\game\ex1", @"Z:\tgame\game", client, patchFileBaseUrls[2], source.Token),
                Test_Single(2, @"Z:\patch-dl.ffxiv.com\game\ex2", @"Z:\tgame\game", client, patchFileBaseUrls[3], source.Token),
                Test_Single(3, @"Z:\patch-dl.ffxiv.com\game\ex3", @"Z:\tgame\game", client, patchFileBaseUrls[4] , source.Token),
                Test_Single(4, @"Z:\patch-dl.ffxiv.com\game\ex4", @"Z:\tgame\game", client, patchFileBaseUrls[5], source.Token),
            });
        }
    }
}
