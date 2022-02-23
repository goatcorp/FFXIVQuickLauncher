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

        private readonly List<string> sourceFiles = new();
        private readonly List<int> sourceFileLastPtr = new();
        private readonly List<IndexedZiPatchTargetFile> targetFiles = new();
        private readonly List<IList<Tuple<int, int>>> sourceFilePartsCache = new();

        public IndexedZiPatchIndex(int expacVersion)
        {
            ExpacVersion = expacVersion;
        }

        public IndexedZiPatchIndex(BinaryReader reader, bool disposeReader = true)
        {
            try
            {
                ExpacVersion = reader.ReadInt32();

                for (int i = 0, readIndex = reader.ReadInt32(); i < readIndex; i++)
                    this.sourceFiles.Add(reader.ReadString());
                foreach (var _ in this.sourceFiles)
                    this.sourceFileLastPtr.Add(reader.ReadInt32());

                for (int i = 0, readIndex = reader.ReadInt32(); i < readIndex; i++)
                    this.targetFiles.Add(new IndexedZiPatchTargetFile(reader, false));
            }
            finally
            {
                if (disposeReader)
                {
                    reader.Dispose();
                }
            }
        }

        public IList<string> Sources => this.sourceFiles.AsReadOnly();
        public int GetSourceLastPtr(int index) => this.sourceFileLastPtr[index];
        public IList<IndexedZiPatchTargetFile> Targets => this.targetFiles.AsReadOnly();

        public IList<IList<Tuple<int, int>>> SourceParts
        {
            get
            {
                for (var sourceFileIndex = this.sourceFilePartsCache.Count; sourceFileIndex < this.sourceFiles.Count; sourceFileIndex++)
                {
                    var list = new List<Tuple<int, int>>();
                    for (var i = 0; i < this.targetFiles.Count; i++)
                        for (var j = 0; j < this.targetFiles[i].Count; j++)
                            if (this.targetFiles[i][j].SourceIndex == sourceFileIndex)
                                list.Add(Tuple.Create(i, j));
                    list.Sort((x, y) => this.targetFiles[x.Item1][x.Item2].SourceOffset.CompareTo(this.targetFiles[y.Item1][y.Item2].SourceOffset));
                    this.sourceFilePartsCache.Add(list.AsReadOnly());
                }

                return this.sourceFilePartsCache.AsReadOnly();
            }
        }

        public IndexedZiPatchTargetFile this[int index] => this.targetFiles[index];
        public IndexedZiPatchTargetFile this[string name] => this.targetFiles[IndexOf(name)];
        public int IndexOf(string name) => this.targetFiles.FindIndex(x => x.RelativePath == NormalizePath(name));
        public int Length => this.targetFiles.Count;
        public string VersionName => this.sourceFiles.Last().Substring(1, this.sourceFiles.Last().Length - 7);
        public string VersionFileBase => ExpacVersion == EXPAC_VERSION_BOOT ? "ffxivboot" : ExpacVersion == EXPAC_VERSION_BASE_GAME ? "ffxivgame" : $"sqpack/ex{ExpacVersion}/ex{ExpacVersion}";
        public string VersionFileVer => VersionFileBase + ".ver";
        public string VersionFileBck => VersionFileBase + ".bck";

        private void ReassignTargetIndices()
        {
            for (int i = 0; i < this.targetFiles.Count; i++)
            {
                for (var j = 0; j < this.targetFiles[i].Count; j++)
                {
                    var obj = this.targetFiles[i][j];
                    obj.TargetIndex = i;
                    this.targetFiles[i][j] = obj;
                }
            }
        }

        private Tuple<int, IndexedZiPatchTargetFile> AllocFile(string target)
        {
            target = NormalizePath(target);
            var targetFileIndex = IndexOf(target);
            if (targetFileIndex == -1)
            {
                this.targetFiles.Add(new(target));
                targetFileIndex = this.targetFiles.Count - 1;
            }
            return Tuple.Create(targetFileIndex, this.targetFiles[targetFileIndex]);
        }

        public async Task ApplyZiPatch(string patchFileName, ZiPatchFile patchFile, CancellationToken? cancellationToken = null)
        {
            await Task.Run(() =>
            {
                var sourceIndex = this.sourceFiles.Count;
                this.sourceFiles.Add(patchFileName);
                this.sourceFileLastPtr.Add(0);
                this.sourceFilePartsCache.Clear();

                var platform = ZiPatchConfig.PlatformId.Win32;
                foreach (var patchChunk in patchFile.GetChunks())
                {
                    if (cancellationToken.HasValue)
                        cancellationToken.Value.ThrowIfCancellationRequested();

                    if (patchChunk is DeleteDirectoryChunk deleteDirectoryChunk)
                    {
                        var prefix = NormalizePath(deleteDirectoryChunk.DirName.ToLowerInvariant());
                        this.targetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant().StartsWith(prefix));
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
                                            SourceIndex = sourceIndex,
                                            SourceOffset = dataOffset,
                                            IsDeflatedBlockData = true,
                                        });
                                        this.sourceFileLastPtr[this.sourceFileLastPtr.Count - 1] = dataOffset + block.CompressedSize;
                                    }
                                    else
                                    {
                                        file.Update(new IndexedZiPatchPartLocator
                                        {
                                            TargetOffset = offset,
                                            TargetSize = block.DecompressedSize,
                                            TargetIndex = targetIndex,
                                            SourceIndex = sourceIndex,
                                            SourceOffset = dataOffset,
                                        });
                                        this.sourceFileLastPtr[this.sourceFileLastPtr.Count - 1] = dataOffset + block.DecompressedSize;
                                    }
                                    offset += block.DecompressedSize;
                                }

                                break;

                            case SqpkFile.OperationKind.RemoveAll:
                                var xpacPath = SqexFile.GetExpansionFolder((byte)sqpkFile.ExpansionId);

                                this.targetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant().StartsWith($"sqpack/{xpacPath}"));
                                this.targetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant().StartsWith($"movie/{xpacPath}"));
                                ReassignTargetIndices();
                                break;

                            case SqpkFile.OperationKind.DeleteFile:
                                this.targetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant() == sqpkFile.TargetFile.RelativePath.ToLowerInvariant());
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
                            SourceIndex = sourceIndex,
                            SourceOffset = sqpkAddData.BlockDataSourceOffset,
                            Crc32OrPlaceholderEntryDataUnits = (uint)(sqpkAddData.BlockNumber >> 7) - 1,
                        });
                        this.sourceFileLastPtr[this.sourceFileLastPtr.Count - 1] = (int)(sqpkAddData.BlockDataSourceOffset + sqpkAddData.BlockNumber);
                        file.Update(new IndexedZiPatchPartLocator
                        {
                            TargetOffset = sqpkAddData.BlockOffset + sqpkAddData.BlockNumber,
                            TargetSize = sqpkAddData.BlockDeleteNumber,
                            TargetIndex = targetIndex,
                            SourceIndex = IndexedZiPatchPartLocator.SOURCE_INDEX_ZEROS,
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
                                SourceIndex = IndexedZiPatchPartLocator.SOURCE_INDEX_EMPTY_BLOCK,
                                Crc32OrPlaceholderEntryDataUnits = (uint)sqpkDeleteData.BlockNumber - 1,
                            });
                            if (sqpkDeleteData.BlockNumber > 1)
                            {
                                file.Update(new IndexedZiPatchPartLocator
                                {
                                    TargetOffset = sqpkDeleteData.BlockOffset + (1 << 7),
                                    TargetSize = (sqpkDeleteData.BlockNumber - 1) << 7,
                                    TargetIndex = targetIndex,
                                    SourceIndex = IndexedZiPatchPartLocator.SOURCE_INDEX_ZEROS,
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
                                SourceIndex = IndexedZiPatchPartLocator.SOURCE_INDEX_EMPTY_BLOCK,
                                Crc32OrPlaceholderEntryDataUnits = (uint)sqpkExpandData.BlockNumber - 1,
                            });
                            if (sqpkExpandData.BlockNumber > 1)
                            {
                                file.Update(new IndexedZiPatchPartLocator
                                {
                                    TargetOffset = sqpkExpandData.BlockOffset + (1 << 7),
                                    TargetSize = (sqpkExpandData.BlockNumber - 1) << 7,
                                    TargetIndex = targetIndex,
                                    SourceIndex = IndexedZiPatchPartLocator.SOURCE_INDEX_ZEROS,
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
                            SourceIndex = sourceIndex,
                            SourceOffset = sqpkHeader.HeaderDataSourceOffset,
                        });
                        this.sourceFileLastPtr[this.sourceFileLastPtr.Count - 1] = (int)(sqpkHeader.HeaderDataSourceOffset + SqpkHeader.HEADER_SIZE);
                    }
                }
            });
        }

        public async Task CalculateCrc32(List<Stream> sources, CancellationToken? cancellationToken = null)
        {
            foreach (var file in this.targetFiles)
            {
                if (cancellationToken.HasValue)
                    cancellationToken.Value.ThrowIfCancellationRequested();
                await file.CalculateCrc32(sources, cancellationToken);
            }
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(ExpacVersion);

            writer.Write(this.sourceFiles.Count);
            foreach (var file in this.sourceFiles)
                writer.Write(file);
            foreach (var file in this.sourceFileLastPtr)
                writer.Write(file);

            writer.Write(this.targetFiles.Count);
            foreach (var file in this.targetFiles)
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