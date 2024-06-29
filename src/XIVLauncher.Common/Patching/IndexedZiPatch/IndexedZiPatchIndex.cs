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

#nullable enable

namespace XIVLauncher.Common.Patching.IndexedZiPatch;

public class IndexedZiPatchIndex
{
    public const uint FileSignature = 0x89AA3CD1;
    public const uint FileVersion = 2;

    public const int ExpacVersionBoot = -1;
    public const int ExpacVersionBaseGame = 0;

    public readonly int ExpacVersion;

    private readonly List<string> sourceFiles = [];
    private readonly List<long> sourceFileLastPtr = [];
    private readonly List<IndexedZiPatchTargetFile> targetFiles = [];
    private readonly List<IList<Tuple<int, int>>> sourceFilePartsCache = [];

    public IndexedZiPatchIndex(int expacVersion)
    {
        ExpacVersion = expacVersion;
    }

    public IndexedZiPatchIndex(BinaryReader reader, bool disposeReader = true)
    {
        try
        {
            if (reader.ReadUInt32() != FileSignature)
                throw new InvalidDataException("Not a valid ZiPatch index file.");
            if (reader.ReadUInt32() != FileVersion)
                throw new InvalidDataException("Not a valid ZiPatch index file version.");

            ExpacVersion = reader.ReadInt32();

            for (int i = 0, readIndex = reader.ReadInt32(); i < readIndex; i++)
                this.sourceFiles.Add(reader.ReadString());
            foreach (var _ in this.sourceFiles)
                this.sourceFileLastPtr.Add(reader.ReadInt64());

            for (int i = 0, readIndex = reader.ReadInt32(); i < readIndex; i++)
                this.targetFiles.Add(new(reader, false));
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
    public long GetSourceLastPtr(int index) => this.sourceFileLastPtr[index];
    public IList<IndexedZiPatchTargetFile> Targets => this.targetFiles.AsReadOnly();

    public IList<IList<Tuple<int, int>>> SourceParts
    {
        get
        {
            for (var sourceFileIndex = this.sourceFilePartsCache.Count; sourceFileIndex < this.sourceFiles.Count; sourceFileIndex++)
            {
                var list = new List<Tuple<int, int>>();

                for (var i = 0; i < this.targetFiles.Count; i++)
                {
                    for (var j = 0; j < this.targetFiles[i].Count; j++)
                    {
                        if (this.targetFiles[i][j].SourceIndex == sourceFileIndex)
                            list.Add(Tuple.Create(i, j));
                    }
                }

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
    public string VersionFileBase => ExpacVersion == ExpacVersionBoot ? "ffxivboot" : ExpacVersion == ExpacVersionBaseGame ? "ffxivgame" : $"sqpack/ex{ExpacVersion}/ex{ExpacVersion}";
    public string VersionFileVer => VersionFileBase + ".ver";
    public string VersionFileBck => VersionFileBase + ".bck";

    private void ReassignTargetIndices()
    {
        for (var i = 0; i < this.targetFiles.Count; i++)
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

    public async Task ApplyZiPatch(string patchFileName, ZiPatchFile patchFile, CancellationToken cancellationToken = default)
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
                cancellationToken.ThrowIfCancellationRequested();

                switch (patchChunk)
                {
                    case DeleteDirectoryChunk deleteDirectoryChunk:
                    {
                        var prefix = NormalizePath(deleteDirectoryChunk.DirName.ToLowerInvariant());
                        this.targetFiles.RemoveAll(x => x.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                        this.ReassignTargetIndices();
                        break;
                    }

                    case SqpkTargetInfo sqpkTargetInfo:
                        platform = sqpkTargetInfo.Platform;
                        break;

                    case SqpkFile sqpkFile:
                        switch (sqpkFile.Operation)
                        {
                            case SqpkFile.OperationKind.AddFile:
                                var (targetIndex, file) = this.AllocFile(sqpkFile.TargetFile.RelativePath);
                                if (sqpkFile.FileOffset == 0)
                                    file.Clear();

                                var offset = sqpkFile.FileOffset;

                                for (var i = 0; i < sqpkFile.CompressedData.Count; ++i)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    var block = sqpkFile.CompressedData[i];
                                    var dataOffset = sqpkFile.CompressedDataSourceOffsets[i];

                                    if (block.IsCompressed)
                                    {
                                        file.Update(new()
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
                                        file.Update(new()
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

                                this.targetFiles.RemoveAll(x => x.RelativePath.StartsWith($"sqpack/{xpacPath}", StringComparison.OrdinalIgnoreCase));
                                this.targetFiles.RemoveAll(x => x.RelativePath.StartsWith($"movie/{xpacPath}", StringComparison.OrdinalIgnoreCase));
                                this.ReassignTargetIndices();
                                break;

                            case SqpkFile.OperationKind.DeleteFile:
                                this.targetFiles.RemoveAll(x => x.RelativePath.ToLowerInvariant() == sqpkFile.TargetFile.RelativePath.ToLowerInvariant());
                                this.ReassignTargetIndices();
                                break;
                        }

                        break;

                    case SqpkAddData sqpkAddData:
                    {
                        sqpkAddData.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = this.AllocFile(sqpkAddData.TargetFile.RelativePath);
                        file.Update(new()
                        {
                            TargetOffset = sqpkAddData.BlockOffset,
                            TargetSize = sqpkAddData.BlockNumber,
                            TargetIndex = targetIndex,
                            SourceIndex = sourceIndex,
                            SourceOffset = sqpkAddData.BlockDataSourceOffset,
                            Crc32OrPlaceholderEntryDataUnits = (uint)(sqpkAddData.BlockNumber >> 7) - 1,
                        });
                        this.sourceFileLastPtr[this.sourceFileLastPtr.Count - 1] = (int)(sqpkAddData.BlockDataSourceOffset + sqpkAddData.BlockNumber);
                        file.Update(new()
                        {
                            TargetOffset = sqpkAddData.BlockOffset + sqpkAddData.BlockNumber,
                            TargetSize = sqpkAddData.BlockDeleteNumber,
                            TargetIndex = targetIndex,
                            SourceIndex = IndexedZiPatchPartLocator.SourceIndexZeros,
                            Crc32OrPlaceholderEntryDataUnits = (uint)(sqpkAddData.BlockDeleteNumber >> 7) - 1,
                        });
                        break;
                    }

                    case SqpkDeleteData sqpkDeleteData:
                    {
                        sqpkDeleteData.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = this.AllocFile(sqpkDeleteData.TargetFile.RelativePath);

                        if (sqpkDeleteData.BlockNumber > 0)
                        {
                            file.Update(new()
                            {
                                TargetOffset = sqpkDeleteData.BlockOffset,
                                TargetSize = 1 << 7,
                                TargetIndex = targetIndex,
                                SourceIndex = IndexedZiPatchPartLocator.SourceIndexEmptyBlock,
                                Crc32OrPlaceholderEntryDataUnits = (uint)sqpkDeleteData.BlockNumber - 1,
                            });

                            if (sqpkDeleteData.BlockNumber > 1)
                            {
                                file.Update(new()
                                {
                                    TargetOffset = sqpkDeleteData.BlockOffset + (1 << 7),
                                    TargetSize = (sqpkDeleteData.BlockNumber - 1) << 7,
                                    TargetIndex = targetIndex,
                                    SourceIndex = IndexedZiPatchPartLocator.SourceIndexZeros,
                                });
                            }
                        }

                        break;
                    }

                    case SqpkExpandData sqpkExpandData:
                    {
                        sqpkExpandData.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = this.AllocFile(sqpkExpandData.TargetFile.RelativePath);

                        if (sqpkExpandData.BlockNumber > 0)
                        {
                            file.Update(new()
                            {
                                TargetOffset = sqpkExpandData.BlockOffset,
                                TargetSize = 1 << 7,
                                TargetIndex = targetIndex,
                                SourceIndex = IndexedZiPatchPartLocator.SourceIndexEmptyBlock,
                                Crc32OrPlaceholderEntryDataUnits = (uint)sqpkExpandData.BlockNumber - 1,
                            });

                            if (sqpkExpandData.BlockNumber > 1)
                            {
                                file.Update(new()
                                {
                                    TargetOffset = sqpkExpandData.BlockOffset + (1 << 7),
                                    TargetSize = (sqpkExpandData.BlockNumber - 1) << 7,
                                    TargetIndex = targetIndex,
                                    SourceIndex = IndexedZiPatchPartLocator.SourceIndexZeros,
                                });
                            }
                        }

                        break;
                    }

                    case SqpkHeader sqpkHeader:
                    {
                        sqpkHeader.TargetFile.ResolvePath(platform);
                        var (targetIndex, file) = this.AllocFile(sqpkHeader.TargetFile.RelativePath);
                        file.Update(new()
                        {
                            TargetOffset = sqpkHeader.HeaderKind == SqpkHeader.TargetHeaderKind.Version ? 0 : SqpkHeader.HEADER_SIZE,
                            TargetSize = SqpkHeader.HEADER_SIZE,
                            TargetIndex = targetIndex,
                            SourceIndex = sourceIndex,
                            SourceOffset = sqpkHeader.HeaderDataSourceOffset,
                        });
                        this.sourceFileLastPtr[this.sourceFileLastPtr.Count - 1] = (int)(sqpkHeader.HeaderDataSourceOffset + SqpkHeader.HEADER_SIZE);
                        break;
                    }
                }
            }
        }, cancellationToken);
    }

    public async Task CalculateCrc32(List<Stream> sources, CancellationToken cancellationToken = default)
    {
        foreach (var file in this.targetFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await file.CalculateCrc32(sources, cancellationToken);
        }
    }

    public void WriteTo(BinaryWriter writer)
    {
        writer.Write(FileSignature);
        writer.Write(FileVersion);
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
