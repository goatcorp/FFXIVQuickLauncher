using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.ZiPatch;
using XIVLauncher.Common.Patching.ZiPatch.Chunk;
using XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    public class IndexedZiPatchIndex
    {
        public const int EXPAC_VERSION_BOOT = -1;
        public const int EXPAC_VERSION_BASE_GAME = 0;

        public readonly int ExpacVersion;

        private readonly List<string> SourceFiles = new();
        private readonly List<int> SourceFileLastPtr = new();
        private readonly List<IndexedZiPatchTargetFile> TargetFiles = new();
        private readonly List<IList<Tuple<int, int>>> SourceFilePartsCache = new();

        public IndexedZiPatchIndex(int expacVersion)
        {
            ExpacVersion = expacVersion;
        }

        public IndexedZiPatchIndex(BinaryReader reader, bool disposeReader = true)
        {
            try
            {
                ExpacVersion = reader.ReadInt32();

                for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
                    SourceFiles.Add(reader.ReadString());
                foreach (var _ in SourceFiles)
                    SourceFileLastPtr.Add(reader.ReadInt32());

                for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
                    TargetFiles.Add(new IndexedZiPatchTargetFile(reader, false));
            }
            finally
            {
                if (disposeReader)
                {
                    reader.Dispose();
                }
            }
        }

        public IList<string> Sources => SourceFiles.AsReadOnly();
        public int GetSourceLastPtr(int index) => SourceFileLastPtr[index];
        public IList<IndexedZiPatchTargetFile> Targets => TargetFiles.AsReadOnly();
        public IList<IList<Tuple<int, int>>> SourceParts
        {
            get
            {
                for (var sourceFileIndex = SourceFilePartsCache.Count; sourceFileIndex < SourceFiles.Count; sourceFileIndex++)
                {
                    var list = new List<Tuple<int, int>>();
                    for (var i = 0; i < TargetFiles.Count; i++)
                        for (var j = 0; j < TargetFiles[i].Count; j++)
                            if (TargetFiles[i][j].SourceIndex == sourceFileIndex)
                                list.Add(Tuple.Create(i, j));
                    list.Sort((x, y) => TargetFiles[x.Item1][x.Item2].SourceOffset.CompareTo(TargetFiles[y.Item1][y.Item2].SourceOffset));
                    SourceFilePartsCache.Add(list.AsReadOnly());
                }
                return SourceFilePartsCache.AsReadOnly();
            }
        }
        public IndexedZiPatchTargetFile this[int index] => TargetFiles[index];
        public IndexedZiPatchTargetFile this[string name] => TargetFiles[IndexOf(name)];
        public int IndexOf(string name) => TargetFiles.FindIndex(x => x.RelativePath == NormalizePath(name));
        public int Length => TargetFiles.Count;
        public string VersionName => SourceFiles.Last().Substring(1, SourceFiles.Last().Length - 7);
        public string VersionFileBase => ExpacVersion == EXPAC_VERSION_BOOT ? "ffxivboot" : ExpacVersion == EXPAC_VERSION_BASE_GAME ? "ffxivgame" : $"sqpack/ex{ExpacVersion}/ex{ExpacVersion}";
        public string VersionFileVer => VersionFileBase + ".ver";
        public string VersionFileBck => VersionFileBase + ".bck";

        private void ReassignTargetIndices()
        {
            for (int i = 0; i < TargetFiles.Count; i++)
            {
                for (var j = 0; j < TargetFiles[i].Count; j++)
                {
                    var obj = TargetFiles[i][j];
                    obj.TargetIndex = i;
                    TargetFiles[i][j] = obj;
                }
            }
        }

        private Tuple<int, IndexedZiPatchTargetFile> AllocFile(string target)
        {
            target = NormalizePath(target);
            var targetFileIndex = IndexOf(target);
            if (targetFileIndex == -1)
            {
                TargetFiles.Add(new(target));
                targetFileIndex = TargetFiles.Count - 1;
            }
            return Tuple.Create(targetFileIndex, TargetFiles[targetFileIndex]);
        }

        public async Task ApplyZiPatch(string patchFileName, ZiPatchFile patchFile, CancellationToken? cancellationToken = null)
        {
            await Task.Run(() =>
            {
                var SourceIndex = SourceFiles.Count;
                SourceFiles.Add(patchFileName);
                SourceFileLastPtr.Add(0);
                SourceFilePartsCache.Clear();

                var platform = ZiPatchConfig.PlatformId.Win32;
                foreach (var patchChunk in patchFile.GetChunks())
                {
                    if (cancellationToken.HasValue)
                        cancellationToken.Value.ThrowIfCancellationRequested();

                    if (patchChunk is DeleteDirectoryChunk deleteDirectoryChunk)
                    {
                        var prefix = NormalizePath(deleteDirectoryChunk.DirName.ToLowerInvariant());
                        TargetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant().StartsWith(prefix));
                        ReassignTargetIndices();
                    }
                    else if (patchChunk is SqpkTargetInfo sqpkTargetInfo)
                    {
                        platform = sqpkTargetInfo.Platform;
                    }
                    else if (patchChunk is SqpkFile sqpkFile)
                    {
                        switch (sqpkFile.Operation)
                        {
                            case SqpkFile.OperationKind.AddFile:
                                var (targetIndex, file) = AllocFile(sqpkFile.TargetFile.RelativePath);
                                if (sqpkFile.FileOffset == 0)
                                    file.Clear();

                                var offset = sqpkFile.FileOffset;
                                for (var i = 0; i < sqpkFile.CompressedData.Count; ++i)
                                {
                                    if (cancellationToken.HasValue)
                                        cancellationToken.Value.ThrowIfCancellationRequested();

                                    var block = sqpkFile.CompressedData[i];
                                    var dataOffset = (int)sqpkFile.CompressedDataSourceOffsets[i];
                                    if (block.IsCompressed)
                                    {
                                        file.Update(new IndexedZiPatchPartLocator
                                        {
                                            TargetOffset = offset,
                                            TargetSize = block.DecompressedSize,
                                            TargetIndex = targetIndex,
                                            SourceIndex = SourceIndex,
                                            SourceOffset = dataOffset,
                                            IsDeflatedBlockData = true,
                                        });
                                        SourceFileLastPtr[SourceFileLastPtr.Count - 1] = dataOffset + block.CompressedSize;
                                    }
                                    else
                                    {
                                        file.Update(new IndexedZiPatchPartLocator
                                        {
                                            TargetOffset = offset,
                                            TargetSize = block.DecompressedSize,
                                            TargetIndex = targetIndex,
                                            SourceIndex = SourceIndex,
                                            SourceOffset = dataOffset,
                                        });
                                        SourceFileLastPtr[SourceFileLastPtr.Count - 1] = dataOffset + block.DecompressedSize;
                                    }
                                    offset += block.DecompressedSize;
                                }

                                break;

                            case SqpkFile.OperationKind.RemoveAll:
                                var xpacPath = SqexFile.GetExpansionFolder((byte)sqpkFile.ExpansionId);

                                TargetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant().StartsWith($"sqpack/{xpacPath}"));
                                TargetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant().StartsWith($"movie/{xpacPath}"));
                                ReassignTargetIndices();
                                break;

                            case SqpkFile.OperationKind.DeleteFile:
                                TargetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant() == sqpkFile.TargetFile.RelativePath.ToLowerInvariant());
                                ReassignTargetIndices();
                                break;
                        }
                    }
                    else if (patchChunk is SqpkAddData sqpkAddData)
                    {
                        sqpkAddData.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = AllocFile(sqpkAddData.TargetFile.RelativePath);
                        file.Update(new IndexedZiPatchPartLocator
                        {
                            TargetOffset = sqpkAddData.BlockOffset,
                            TargetSize = sqpkAddData.BlockNumber,
                            TargetIndex = targetIndex,
                            SourceIndex = SourceIndex,
                            SourceOffset = sqpkAddData.BlockDataSourceOffset,
                            Crc32OrPlaceholderEntryDataUnits = (uint)(sqpkAddData.BlockNumber >> 7) - 1,
                        });
                        SourceFileLastPtr[SourceFileLastPtr.Count - 1] = (int)(sqpkAddData.BlockDataSourceOffset + sqpkAddData.BlockNumber);
                        file.Update(new IndexedZiPatchPartLocator
                        {
                            TargetOffset = sqpkAddData.BlockOffset + sqpkAddData.BlockNumber,
                            TargetSize = sqpkAddData.BlockDeleteNumber,
                            TargetIndex = targetIndex,
                            SourceIndex = IndexedZiPatchPartLocator.SourceIndex_Zeros,
                            Crc32OrPlaceholderEntryDataUnits = (uint)(sqpkAddData.BlockDeleteNumber >> 7) - 1,
                        });
                    }
                    else if (patchChunk is SqpkDeleteData sqpkDeleteData)
                    {
                        sqpkDeleteData.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = AllocFile(sqpkDeleteData.TargetFile.RelativePath);
                        if (sqpkDeleteData.BlockNumber > 0)
                        {
                            file.Update(new IndexedZiPatchPartLocator
                            {
                                TargetOffset = sqpkDeleteData.BlockOffset,
                                TargetSize = 1 << 7,
                                TargetIndex = targetIndex,
                                SourceIndex = IndexedZiPatchPartLocator.SourceIndex_EmptyBlock,
                                Crc32OrPlaceholderEntryDataUnits = (uint)sqpkDeleteData.BlockNumber - 1,
                            });
                            if (sqpkDeleteData.BlockNumber > 1)
                            {
                                file.Update(new IndexedZiPatchPartLocator
                                {
                                    TargetOffset = sqpkDeleteData.BlockOffset + (1 << 7),
                                    TargetSize = (sqpkDeleteData.BlockNumber - 1) << 7,
                                    TargetIndex = targetIndex,
                                    SourceIndex = IndexedZiPatchPartLocator.SourceIndex_Zeros,
                                });
                            }
                        }
                    }
                    else if (patchChunk is SqpkExpandData sqpkExpandData)
                    {
                        sqpkExpandData.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = AllocFile(sqpkExpandData.TargetFile.RelativePath);
                        if (sqpkExpandData.BlockNumber > 0)
                        {
                            file.Update(new IndexedZiPatchPartLocator
                            {
                                TargetOffset = sqpkExpandData.BlockOffset,
                                TargetSize = 1 << 7,
                                TargetIndex = targetIndex,
                                SourceIndex = IndexedZiPatchPartLocator.SourceIndex_EmptyBlock,
                                Crc32OrPlaceholderEntryDataUnits = (uint)sqpkExpandData.BlockNumber - 1,
                            });
                            if (sqpkExpandData.BlockNumber > 1)
                            {
                                file.Update(new IndexedZiPatchPartLocator
                                {
                                    TargetOffset = sqpkExpandData.BlockOffset + (1 << 7),
                                    TargetSize = (sqpkExpandData.BlockNumber - 1) << 7,
                                    TargetIndex = targetIndex,
                                    SourceIndex = IndexedZiPatchPartLocator.SourceIndex_Zeros,
                                });
                            }
                        }
                    }
                    else if (patchChunk is SqpkHeader sqpkHeader)
                    {
                        sqpkHeader.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = AllocFile(sqpkHeader.TargetFile.RelativePath);
                        file.Update(new IndexedZiPatchPartLocator
                        {
                            TargetOffset = sqpkHeader.HeaderKind == SqpkHeader.TargetHeaderKind.Version ? 0 : SqpkHeader.HEADER_SIZE,
                            TargetSize = SqpkHeader.HEADER_SIZE,
                            TargetIndex = targetIndex,
                            SourceIndex = SourceIndex,
                            SourceOffset = sqpkHeader.HeaderDataSourceOffset,
                        });
                        SourceFileLastPtr[SourceFileLastPtr.Count - 1] = (int)(sqpkHeader.HeaderDataSourceOffset + SqpkHeader.HEADER_SIZE);
                    }
                }
            });
        }

        public async Task CalculateCrc32(List<Stream> sources, CancellationToken? cancellationToken = null)
        {
            foreach (var file in TargetFiles)
            {
                if (cancellationToken.HasValue)
                    cancellationToken.Value.ThrowIfCancellationRequested();
                await file.CalculateCrc32(sources, cancellationToken);
            }
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(ExpacVersion);

            writer.Write(SourceFiles.Count);
            foreach (var file in SourceFiles)
                writer.Write(file);
            foreach (var file in SourceFileLastPtr)
                writer.Write(file);

            writer.Write(TargetFiles.Count);
            foreach (var file in TargetFiles)
                file.WriteTo(writer);
        }

        private static string NormalizePath(string path)
        {
            if (path == "")
                return path;
            path = path.Replace("\\", "/");
            while (path[0] == '/')
                path = path.Substring(1);
            return path;
        }
    }
}