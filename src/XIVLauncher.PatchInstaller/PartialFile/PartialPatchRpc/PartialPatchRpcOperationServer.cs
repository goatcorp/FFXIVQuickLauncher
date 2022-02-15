using Serilog;
using SharedMemory;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace XIVLauncher.PatchInstaller.PartialFile.PartialPatchRpc
{
    class PartialPatchRpcOperationServer : IDisposable
    {
        private readonly RpcBuffer Rpc;
        private readonly Queue<BinaryReader> QueuedMessages = new();
        private bool Finished = false;

        private class PendingFile : IDisposable
        {
            internal readonly string TargetPath;
            internal readonly short TargetIndex;
            internal readonly PartialFilePartList PartList;
            internal readonly HashSet<int> PendingPartIndices = new();

            private Stream OpenStream;
            internal Stream Stream
            {
                get
                {
                    if (OpenStream == null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(TargetPath));
                        OpenStream = new FileStream(TargetPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    }
                    return OpenStream;
                }
            }

            public long FileSize => PartList.FileSize;

            internal PendingFile(short targetIndex, string targetPath, PartialFilePartList partList)
            {
                TargetIndex = targetIndex;
                TargetPath = targetPath;
                PartList = partList;
            }

            public void CloseStream()
            {
                OpenStream?.Dispose();
                OpenStream = null;
            }

            public void Dispose()
            {
                CloseStream();
            }
        }

        private class PatchSet : IDisposable
        {
            internal string RootPath;
            internal PartialFileDef Definition;
            internal PartialFileVerification Verification;
            internal string VersionFileName;
            internal List<PendingFile> Files;

            internal float VerifyProgress = 0;
            internal long ApplyProgress = 0;
            internal long ApplyMax = 1;

            internal string VersionFilePath => Path.Combine(RootPath, VersionFileName + ".ver");
            internal string VersionFileBckPath => Path.Combine(RootPath, VersionFileName + ".bck");
            internal string VersionFileDirectory => Path.GetDirectoryName(Path.Combine(RootPath, VersionFileName));

            public void Dispose()
            {
                foreach (var file in Files)
                    file.Dispose();
            }
        }
        private readonly List<PatchSet> PatchSets = new();

        public int ProgressReportInterval = 250;
        private readonly Thread ProgressUpdater;

        internal PartialPatchRpcOperationServer(string channelName)
        {
            Rpc = new RpcBuffer(channelName, OnMessage);
            ProgressUpdater = new Thread(ProgressUpdaterBody);
        }

        private void OnMessage(ulong msgId, byte[] payload)
        {
            lock (QueuedMessages)
            {
                while (QueuedMessages.Count >= 512)
                    Monitor.Wait(QueuedMessages);

                QueuedMessages.Enqueue(new BinaryReader(new MemoryStream(payload)));
                Monitor.Pulse(QueuedMessages);
            }
        }

        public void Run()
        {
            ProgressUpdater.Start();
            while (true)
            {
                BinaryReader reader;
                lock (QueuedMessages)
                {
                    while (QueuedMessages.Count == 0)
                        Monitor.Wait(QueuedMessages);
                    reader = QueuedMessages.Dequeue();
                }
                if (reader == null)
                    break;

                switch ((PartialPatchRpcOpcode)reader.ReadInt32())
                {
                    case PartialPatchRpcOpcode.ProvideIndexFile:
                        OnIndexFileProvided(reader);
                        break;

                    case PartialPatchRpcOpcode.ProvidePartialFile:
                        OnPartialFileProvided(reader);
                        break;

                    case PartialPatchRpcOpcode.ProvideIndexFileFinish:
                        OnIndexFileProvideFinished();
                        break;
                }
            }
        }

        private void OnIndexFileProvided(BinaryReader reader)
        {
            var rootPath = reader.ReadString();  // "C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\boot"
            var versionFileName = reader.ReadString();  // "ffxivboot", "ffxivgame", "ex1", etc.
            using var indexStream = new BinaryReader(new DeflateStream(reader.BaseStream, CompressionMode.Decompress));

            var definition = new PartialFileDef();
            definition.ReadFrom(indexStream);
            PatchSets.Add(new PatchSet
            {
                RootPath = rootPath,
                Definition = definition,
                Verification = new PartialFileVerification(definition) { ProgressReportInterval = 250 },
                VersionFileName = versionFileName,
                Files = new(definition.GetFiles().Count),
            });
        }

        private void OnIndexFileProvideFinished()
        {
            for (var patchSetIndex = 0; patchSetIndex < PatchSets.Count; patchSetIndex++)
            {
                var patchSet = PatchSets[patchSetIndex];

                Directory.CreateDirectory(patchSet.VersionFileDirectory);
                using (var versionFile1 = new StreamWriter(new FileStream(patchSet.VersionFilePath, FileMode.Create, FileAccess.Write)))
                using (var versionFile2 = new StreamWriter(new FileStream(patchSet.VersionFileBckPath, FileMode.Create, FileAccess.Write)))
                {
                    var versionStr = patchSet.Definition.GetSourceFiles().Last().Substring(1);
                    versionStr = versionStr.Substring(0, versionStr.Length - 6);
                    versionFile1.Write(versionStr);
                    versionFile2.Write(versionStr);
                }

                long fileSizeSum = 0;
                for (short i = 0; i < patchSet.Definition.GetFileCount(); i++)
                    fileSizeSum += patchSet.Definition.GetFile(i).FileSize;

                long fileSizeProgress = 0;
                for (short i = 0; i < patchSet.Definition.GetFileCount(); i++)
                {
                    var relativePath = patchSet.Definition.GetFileRelativePath(i);
                    var targetFile = new PendingFile(i, Path.Combine(patchSet.RootPath, patchSet.Definition.GetFileRelativePath(i)), patchSet.Definition.GetFile(relativePath));
                    patchSet.Files.Add(targetFile);

                    var remainingErrorMessagesToShow = 8;
                    patchSet.Verification.OnCorruptionFound.Add((string relativePath, PartialFilePart part, PartialFilePart.VerifyDataResult result) =>
                    {
                        switch (result)
                        {
                            case PartialFilePart.VerifyDataResult.FailNotEnoughData:
                                if (remainingErrorMessagesToShow > 0)
                                {
                                    Log.Error("{0}:{1}:{2}: Premature EOF detected", relativePath, part.TargetOffset, targetFile.FileSize);
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
                    patchSet.Verification.OnProgress.Add((long progress, long max) =>
                    {
                        if (progress == 0)
                            Log.Information("[{0}/{1}] Checking file {2}... {3:00.00}", i + 1, patchSet.Definition.GetFileCount(), relativePath, 100.0 * progress / max);
                        patchSet.VerifyProgress = (float)(1.0 * (fileSizeProgress + targetFile.FileSize * ((double)progress / max)) / fileSizeSum);
                        CheckFinished();
                    });
                    patchSet.Verification.VerifyFile(relativePath, targetFile.Stream);
                    patchSet.Verification.OnProgress.Clear();
                    patchSet.Verification.OnCorruptionFound.Clear();

                    if (targetFile.Stream.Length > targetFile.FileSize)
                        targetFile.Stream.SetLength(targetFile.FileSize);

                    if (patchSet.Verification.MissingPartIndicesPerTargetFile[i].Count == 0)
                        targetFile.CloseStream();
                    else
                    {
                        foreach (var partIndex in patchSet.Verification.MissingPartIndicesPerTargetFile[i])
                            patchSet.ApplyMax += targetFile.PartList[partIndex].TargetSize;
                        targetFile.PendingPartIndices.UnionWith(patchSet.Verification.MissingPartIndicesPerTargetFile[i].Where(x => targetFile.PartList[x].IsFromSourceFile));
                    }

                    fileSizeProgress += targetFile.FileSize;
                }

                for (short i = 0; i < patchSet.Verification.MissingPartIndicesPerPatch.Count; i++)
                {
                    if (patchSet.Verification.MissingPartIndicesPerPatch[i].Count == 0)
                        continue;

                    var stream = new MemoryStream();
                    var writer = new BinaryWriter(stream);
                    writer.Write((int)PartialPatchRpcOpcode.RequestPartialFile);
                    writer.Write(patchSetIndex);
                    writer.Write(i);
                    writer.Write(patchSet.Definition.GetSourceFiles()[i]);
                    writer.Write(patchSet.Verification.MissingPartIndicesPerPatch[i].Count);
                    foreach (var partInfo in patchSet.Verification.MissingPartIndicesPerPatch[i])
                    {
                        var targetFileIndex = partInfo.Item1;
                        var partIndex = partInfo.Item2;
                        writer.Write(targetFileIndex);
                        writer.Write(partIndex);
                        writer.Write(patchSet.Definition.GetFile(targetFileIndex)[partIndex].SourceOffset);
                        writer.Write(patchSet.Definition.GetFile(targetFileIndex)[partIndex].SourceSize);
                    }

                    Rpc.RemoteRequest(stream.ToArray());
                }

                PatchSets[patchSetIndex].ApplyProgress += 1;
            }
            CheckFinished();
        }

        private void OnPartialFileProvided(BinaryReader reader)
        {
            var patchSetIndex = reader.ReadInt32();
            var patchSet = PatchSets[patchSetIndex];

            var patchFileIndex = reader.ReadInt16();
            var patchFilePath = reader.ReadString();
            var patchFile = new FileStream(patchFilePath, FileMode.Open, FileAccess.Read);
            Log.Information("Writing from file: {0}", patchFilePath);
            foreach (var item in patchSet.Verification.MissingPartIndicesPerPatch[patchFileIndex])
            {
                var targetFile = patchSet.Files[item.Item1];
                targetFile.PartList[item.Item2].Repair(targetFile.Stream, patchFile);
                PatchSets[patchSetIndex].ApplyProgress += targetFile.PartList[item.Item2].TargetSize;

                targetFile.PendingPartIndices.Remove(item.Item2);
                if (targetFile.PendingPartIndices.Count > 0)
                    continue;

                foreach (var partIndex in patchSet.Verification.MissingPartIndicesPerTargetFile[targetFile.TargetIndex])
                {
                    if (!targetFile.PartList[partIndex].IsFromSourceFile)
                    {
                        targetFile.PartList[partIndex].Repair(targetFile.Stream, (Stream)null);
                        PatchSets[patchSetIndex].ApplyProgress += targetFile.PartList[partIndex].TargetSize;
                    }
                }
                targetFile.Stream.SetLength(targetFile.PartList.FileSize);
                targetFile.CloseStream();
                Log.Information("Done writing: {0}", targetFile.TargetPath);
            }
            {
                var stream = new MemoryStream();
                var writer = new BinaryWriter(stream);
                writer.Write((int)PartialPatchRpcOpcode.FinishPartialFile);
                writer.Write(patchSetIndex);
                writer.Write(patchFileIndex);
                writer.Write(patchFilePath);
                Rpc.RemoteRequest(stream.ToArray());
            }

            CheckFinished();
        }

        private void CheckFinished()
        {
            var empty = true;
            foreach (var patchSet in PatchSets)
            {
                foreach (var targetFile in patchSet.Files)
                {
                    empty &= targetFile.PendingPartIndices != null && targetFile.PendingPartIndices.Count == 0;
                    if (!empty)
                        break;
                }
                empty &= patchSet.ApplyMax == patchSet.ApplyProgress;
                if (!empty)
                    break;
            }
            if (!empty)
                return;

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write((int)PartialPatchRpcOpcode.Finished);
            Rpc.RemoteRequest(stream.ToArray());
            QueuedMessages.Enqueue(null);
            Finished = true;
        }

        private void ProgressUpdaterBody()
        {
            float lastProgress = -1;
            while (!Finished)
            {
                float progressSum = 0;
                long applyProgressSum = 0, applyMaxSum = 0;
                foreach (var patchSet in PatchSets)
                {
                    progressSum += patchSet.VerifyProgress;
                    progressSum += 1f * patchSet.ApplyProgress / patchSet.ApplyMax;
                    applyProgressSum += patchSet.ApplyProgress;
                    applyMaxSum += patchSet.ApplyMax;
                }
                progressSum /= 2 * PatchSets.Count;

                if (progressSum != lastProgress)
                {
                    lastProgress = progressSum;
                    var stream = new MemoryStream();
                    var writer = new BinaryWriter(stream);
                    writer.Write((int)PartialPatchRpcOpcode.StatusUpdate);
                    writer.Write(progressSum);
                    writer.Write(applyProgressSum);
                    writer.Write(applyMaxSum);
                    Rpc.RemoteRequest(stream.ToArray());
                }
                Thread.Sleep(ProgressReportInterval);
            }
        }

        public void Dispose()
        {
            Finished = true;
            foreach (var patchSet in PatchSets)
                patchSet.Dispose();
            PatchSets.Clear();
            ProgressUpdater.Join();
            Rpc.Dispose();
        }
    }
}
