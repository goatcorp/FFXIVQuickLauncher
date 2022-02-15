using Serilog;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.PartialFile.PartialPatchRpc;
using XIVLauncher.PatchInstaller.ZiPatch;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public class PartialPatchOperations
    {
        public static PartialFileDef CreatePatchFileIndices(IList<string> patchFilePaths)
        {
            var sources = new List<Stream>();
            var patchFiles = new List<ZiPatchFile>();
            PartialFileDef fileDef = new();
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
                    var patchFilePath = patchFilePaths[i];
                    sources.Add(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read));
                    patchFiles.Add(new ZiPatchFile(sources[sources.Count - 1]));

                    if (i < firstPatchFileIndex)
                        continue;

                    if (File.Exists(patchFilePath + ".index"))
                    {
                        Log.Information("Reading patch index file {0}...", patchFilePath);
                        using var reader = new BinaryReader(new FileStream(patchFilePath + ".index", FileMode.Open, FileAccess.Read));
                        fileDef.ReadFrom(reader);
                        continue;
                    }

                    Log.Information("Indexing patch file {0}...", patchFilePath);
                    fileDef.ApplyZiPatch(Path.GetFileName(patchFilePath), patchFiles[patchFiles.Count - 1]);

                    Log.Information("Calculating CRC32 for files resulted from patch file {0}...", patchFilePath);
                    fileDef.CalculateCrc32(sources);

                    using (var writer = new BinaryWriter(new FileStream(patchFilePath + ".index.tmp", FileMode.Create)))
                        fileDef.WriteTo(writer);

                    File.Move(patchFilePath + ".index.tmp", patchFilePath + ".index");
                }

                return fileDef;
            }
            finally
            {
                foreach (var source in sources)
                    source.Dispose();
            }
        }

        public static PartialFileVerification VerifyFromPatchFileIndex(IList<string> patchFilePaths, string gameRootPath)
        {
            var def = CreatePatchFileIndices(patchFilePaths);
            var verifier = new PartialFileVerification(def)
            {
                ProgressReportInterval = 1000
            };

            for (var i = 0; i < def.GetFileCount(); i++)
            {
                var relativePath = def.GetFileRelativePath(i);
                var file = def.GetFile(relativePath);
                verifier.OnProgress.Clear();
                verifier.OnCorruptionFound.Clear();
                var remainingErrorMessagesToShow = 8;
                verifier.OnProgress.Add((long progress, long max) => Log.Information("[{0}/{1}] Checking file {2}... {3:00.00}", i + 1, def.GetFileCount(), relativePath, 100.0 * progress / max));
                verifier.OnCorruptionFound.Add((string relativePath, PartialFilePart part, PartialFilePart.VerifyDataResult result) =>
                {
                    switch (result)
                    {
                        case PartialFilePart.VerifyDataResult.FailNotEnoughData:
                            if (remainingErrorMessagesToShow > 0)
                            {
                                Log.Error("{0}:{1}:{2}: Premature EOF detected", relativePath, part.TargetOffset, file.FileSize);
                                remainingErrorMessagesToShow = 0;
                            }
                            break;

                        case PartialFilePart.VerifyDataResult.FailBadData:
                            if (remainingErrorMessagesToShow > 0)
                            {
                                if (--remainingErrorMessagesToShow == 0)
                                    Log.Warning("{0}:{1}:{2}: Corrupt data; suppressing further corruption warnings for this file.", relativePath, part.TargetOffset, part.TargetEnd);
                                else
                                    Log.Warning("{0}:{1}:{2}: Corrupt data", relativePath, part.TargetOffset, part.TargetEnd);
                            }
                            break;
                    }
                });
                try
                {
                    using var local = new FileStream(Path.Combine(gameRootPath, relativePath), FileMode.Open, FileAccess.Read);
                    verifier.VerifyFile(relativePath, local);
                }
                catch (FileNotFoundException)
                {
                    verifier.MarkFileAsMissing(relativePath);
                    Log.Warning("{0}:{1}:{2}: File does not exist", relativePath, 0, file.FileSize);
                }
            }

            return verifier;
        }

        public static void RepairFromPatchFileIndex(IList<string> patchFilePaths, string gameRootPath)
        {
            var verifyResult = VerifyFromPatchFileIndex(patchFilePaths, gameRootPath);
            var sources = new List<Stream>();
            using var disposer = new Util.MultiDisposable();
            foreach (var patchFilePath in patchFilePaths)
                sources.Add(disposer.With(new FileStream(patchFilePath, FileMode.Open, FileAccess.Read)));
            for (short targetFileIndex = 0; targetFileIndex < verifyResult.MissingPartIndicesPerTargetFile.Count; targetFileIndex++)
            {
                var partIndices = verifyResult.MissingPartIndicesPerTargetFile[targetFileIndex];
                if (partIndices.Count == 0 && !verifyResult.TooLongTargetFiles.Contains(targetFileIndex))
                    continue;

                var relativePath = verifyResult.Definition.GetFileRelativePath(targetFileIndex);
                var file = verifyResult.Definition.GetFile(targetFileIndex);

                Log.Information("[{0}/{1}] Repairing file {2}... ({} segments)", targetFileIndex + 1, verifyResult.MissingPartIndicesPerTargetFile.Count, relativePath, partIndices.Count);

                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(gameRootPath, relativePath)));
                using var local = new FileStream(Path.Combine(gameRootPath, relativePath), FileMode.OpenOrCreate, FileAccess.ReadWrite);
                foreach (var partIndex in partIndices)
                    file[partIndex].Repair(local, sources);
                local.SetLength(file.FileSize);
            }
        }

        public static void RepairRpcMode(string channelName)
        {
            using var operations = new PartialPatchRpcOperationServer(channelName);
            operations.Run();
        }

        public static void RpcTest(List<string> args)
        {
            List<List<string>> multiargs = new();
            multiargs.Add(new());
            foreach (var s in args)
            {
                if (s == "--")
                    multiargs.Add(new());
                else
                    multiargs.Last().Add(s);
            }
            var tester = new PartialPatchRpcOperationTestClient();
            foreach (var s in multiargs)
            {
                CreatePatchFileIndices(s.Take(s.Count - 2).ToList());
                tester.AddFiles(s.Take(s.Count - 2).ToList(), s[s.Count - 2], s[s.Count - 1]);
            }
            tester.Run();
        }
    }
}
