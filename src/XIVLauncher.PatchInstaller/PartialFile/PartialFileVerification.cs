using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public class PartialFileVerification
    {
        public readonly PartialFileDef Definition;
        public readonly List<SortedSet<Tuple<int, int>>> MissingPartIndicesPerPatch = new();
        public readonly List<SortedSet<int>> MissingPartIndicesPerTargetFile = new();
        public readonly SortedSet<int> TooLongTargetFiles = new();

        public int ProgressReportInterval = 250;
        private int LastProgressUpdateReport = 0;
        private List<Stream> TargetStreams = new();

        public delegate void OnCorruptionFoundDelegate(string relativePath, PartialFilePart part, PartialFilePart.VerifyDataResult result);
        public delegate void OnProgressDelegate(long progress, long max);

        public readonly List<OnCorruptionFoundDelegate> OnCorruptionFound = new();
        public readonly List<OnProgressDelegate> OnProgress = new();

        public PartialFileVerification(PartialFileDef def)
        {
            Definition = def;
            foreach (var _ in def.GetFiles())
            {
                MissingPartIndicesPerTargetFile.Add(new());
                TargetStreams.Add(null);
            }
            foreach (var _ in def.GetSourceFiles())
                MissingPartIndicesPerPatch.Add(new());
        }

        private void TriggerOnProgress(long progress, long max, bool forceNotify)
        {
            if (!forceNotify)
            {
                if (LastProgressUpdateReport >= 0 && Environment.TickCount < 0)
                {
                    // Overflowed; just report again
                }
                else if (LastProgressUpdateReport + ProgressReportInterval > Environment.TickCount)
                {
                    return;
                }
            }

            LastProgressUpdateReport = Environment.TickCount;
            foreach (var d in OnProgress)
                d(progress, max);
        }

        private void TriggerOnCorruptionFound(string relativePath, PartialFilePart part, PartialFilePart.VerifyDataResult result)
        {
            foreach (var d in OnCorruptionFound)
                d(relativePath, part, result);
        }

        public void VerifyFile(string relativePath, Stream local)
        {
            var targetFileIndex = Definition.GetFileIndex(relativePath);
            var file = Definition.GetFile(relativePath);

            TriggerOnProgress(0, file.FileSize, true);
            for (var i = 0; i < file.Count; ++i)
            {
                TriggerOnProgress(file[i].TargetOffset, file.FileSize, false);
                var verifyResult = file[i].Verify(local);
                switch (verifyResult)
                {
                    case PartialFilePart.VerifyDataResult.Pass:
                        continue;

                    case PartialFilePart.VerifyDataResult.FailUnverifiable:
                        throw new Exception($"{relativePath}:{file[i].TargetOffset}:{file[i].TargetEnd}: Should not happen; unverifiable due to insufficient source data");

                    case PartialFilePart.VerifyDataResult.FailNotEnoughData:
                    case PartialFilePart.VerifyDataResult.FailBadData:
                        if (file[i].IsFromSourceFile)
                            MissingPartIndicesPerPatch[file[i].SourceIndex].Add(Tuple.Create(targetFileIndex, i));
                        MissingPartIndicesPerTargetFile[targetFileIndex].Add(i);
                        TriggerOnCorruptionFound(relativePath, file[i], verifyResult);
                        break;
                }
            }
            if (local.Length > file.FileSize)
                TooLongTargetFiles.Add(targetFileIndex);
            TriggerOnProgress(file.FileSize, file.FileSize, true);
        }

        public void MarkFileAsMissing(string relativePath)
        {
            var targetFileIndex = Definition.GetFileIndex(relativePath);
            var file = Definition.GetFile(relativePath);
            for (var i = 0; i < file.Count; ++i)
            {
                if (file[i].IsFromSourceFile)
                    MissingPartIndicesPerPatch[file[i].SourceIndex].Add(Tuple.Create(targetFileIndex, i));
                MissingPartIndicesPerTargetFile[targetFileIndex].Add(i);
            }
        }

        public void SetTargetStream(int targetIndex, Stream targetStream)
        {
            TargetStreams[targetIndex] = targetStream;
        }

        public async Task<List<PartialFilePart>> RepairFrom(HttpClient client, int patchFileIndex, string uri, ISet<Tuple<int, int>> indicesToRepair)
        {
            var result = new List<PartialFilePart>();
            var offsets = new List<Tuple<long, long>>();
            foreach (var partIndices in indicesToRepair)
            {
                var part = Definition.GetFile(partIndices.Item1)[partIndices.Item2];
                offsets.Add(Tuple.Create(part.SourceOffset, part.MaxSourceEnd));
            }
            offsets.Sort();

            for (int j = 1; j < offsets.Count;)
            {
                if (offsets[j].Item1 - offsets[j - 1].Item2 < 128)
                {
                    offsets[j - 1] = Tuple.Create(offsets[j - 1].Item1, offsets[j].Item2);
                    offsets.RemoveAt(j);
                }
                else
                    j += 1;
            }

            long totalTargetSize = 0;
            long currentTargetSize = 0;
            foreach (var partIndices in MissingPartIndicesPerPatch[patchFileIndex])
                totalTargetSize += Definition.GetFile(partIndices.Item1)[partIndices.Item2].TargetSize;
            TriggerOnProgress(0, totalTargetSize, true);

            var parts = Definition.GetUsedSourceFileParts(patchFileIndex);
            var sourceOffsets = parts.Select(x => Definition.GetFile(x.Item1)[x.Item2].SourceOffset).ToList();

            for (int j = 0; j < offsets.Count; j += 1024)
            {
                var rangeHeader = "bytes=" + string.Join(",", offsets.Skip(j).Take(Math.Min(1024, offsets.Count - j)).Select(x => $"{x.Item1}-{x.Item2 + 1}"));
                using HttpRequestMessage req = new(HttpMethod.Get, uri);
                req.Headers.Add("Range", rangeHeader);
                using var resp = new MultipartRequestHandler(await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead));
                while (true)
                {
                    using var n = await resp.NextPart();
                    if (n == null)
                        break;

                    for (var i = 0; i < parts.Count; i++)
                    {
                        var partFragment = parts[i];
                        if (!MissingPartIndicesPerTargetFile[partFragment.Item1].Contains(partFragment.Item2))
                            continue;

                        var part = Definition.GetFile(partFragment.Item1)[partFragment.Item2];
                        if (part.SourceOffset < n.AvailableFromOffset)
                            continue;
                        if (part.SourceOffset > n.AvailableToOffset)
                            break;

                        var target = TargetStreams[part.TargetIndex];
                        if (target != null)
                        {
                            lock (target)
                            {
                                try
                                {
                                    part.Repair(target, n);
                                }
                                catch (PartialFilePart.InsufficientReconstructionDataException)
                                {
                                    break;
                                }
                            }
                            result.Add(part);
                            currentTargetSize += part.TargetSize;
                            TriggerOnProgress(currentTargetSize, totalTargetSize, false);
                        }
                    }
                }
            }
            TriggerOnProgress(totalTargetSize, totalTargetSize, true);
            return result;
        }

        public void RepairNonPatchData()
        {
            for (int i = 0, i_ = Definition.GetFileCount(); i < i_; i++)
            {
                var target = TargetStreams[i];
                if (target == null)
                    continue;

                var file = Definition.GetFile(i);
                lock (target)
                {
                    foreach (var partIndex in MissingPartIndicesPerTargetFile[i])
                    {
                        var part = file[partIndex];
                        if (part.IsFromSourceFile)
                            continue;

                        part.Repair(target, (Stream)null);
                    }
                    target.SetLength(file.FileSize);
                }
            }
        }

        public static void Test()
        {
            HttpClient client = new();
        }

        public static void TestSingle(HttpClient client, string baseUri, string indexFilePath, string localRootPath, string versionFileName)
        {
            PartialFileDef def = new();
            using var reader = new BinaryReader(new FileStream(indexFilePath, FileMode.Open, FileAccess.Read));
            def.ReadFrom(reader);

            var versionName = def.GetSourceFiles().Last().Substring(1);
            versionName = versionName.Substring(0, versionName.Length - 6);
            var versionNameBytes = Encoding.UTF8.GetBytes(versionName);
            var versionFilePath1 = Path.Combine(localRootPath, versionFileName + ".ver");
            var versionFilePath2 = Path.Combine(localRootPath, versionFileName + ".bck");
            Directory.CreateDirectory(Path.GetDirectoryName(versionFilePath1));
            using (var writer = new FileStream(versionFilePath1, FileMode.Create, FileAccess.Write))
                writer.Write(versionNameBytes, 0, versionNameBytes.Length);
            using (var writer = new FileStream(versionFilePath2, FileMode.Create, FileAccess.Write))
                writer.Write(versionNameBytes, 0, versionNameBytes.Length);

            PartialFileVerification verify = new(def);
            for (int i = 0; i < def.GetFileCount(); i++)
            {
                var relativePath = def.GetFileRelativePath(i);
                var targetPath = Path.Combine(localRootPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                var file = new FileStream(targetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                var remainingErrorMessagesToShow = 8;
                verify.OnCorruptionFound.Add((string relativePath, PartialFilePart part, PartialFilePart.VerifyDataResult result) =>
                {
                    switch (result)
                    {
                        case PartialFilePart.VerifyDataResult.FailNotEnoughData:
                            if (remainingErrorMessagesToShow > 0)
                            {
                                Log.Error("{0}:{1}:{2}: Premature EOF detected", relativePath, part.TargetOffset, def.GetFile(i).FileSize);
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
                verify.OnProgress.Add((long progress, long max) =>
                {
                    if (progress == 0)
                        Log.Information("[{0}/{1}] Checking file {2}... {3:00.00}", i + 1, verify.Definition.GetFileCount(), relativePath, 100.0 * progress / max);
                });
                verify.VerifyFile(def.GetFileRelativePath(i), file);
                verify.OnProgress.Clear();
                verify.OnCorruptionFound.Clear();
                verify.SetTargetStream(i, file);
            }

            verify.OnProgress.Clear();
            verify.OnProgress.Add((long progress, long max) => Log.Information("Writing {0:0.00}/{1:0.00}MB ({2:00.00}%)", progress / 1048576.0, max / 1048576.0, 100.0 * progress / max));
            List<Task<List<PartialFilePart>>> tasks = new();

            const int concCount = 8;
            for (int i = 0, i_ = def.GetSourceFiles().Count; i < i_; i++)
            {
                if (verify.MissingPartIndicesPerPatch[i].Count == 0)
                    continue;

                var uri = $"{baseUri}{def.GetSourceFiles()[i]}";
                var indices = verify.MissingPartIndicesPerPatch[i];
                var indicesPerRequest = Math.Min((int)Math.Ceiling(1.0 * indices.Count / concCount), 1024);
                for (int j = 0; j < indices.Count; j += indicesPerRequest)
                {
                    if (tasks.Count >= concCount)
                    {
                        Task.WaitAny(tasks.ToArray());
                        tasks = tasks.Where(x => !x.IsCompleted).ToList();
                    }
                    tasks.Add(verify.RepairFrom(client, i, uri, indices.Skip(j).Take(Math.Min(indicesPerRequest, indices.Count - j)).ToHashSet()));
                }
            }
            Task.WaitAll(tasks.ToArray());

            verify.RepairNonPatchData();
        }
    }
}
