using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.ZiPatch;
using XIVLauncher.PatchInstaller.ZiPatch.Chunk;
using XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public class PartialFileDef
    {
        private readonly List<string> SourceFiles = new();
        private readonly List<string> TargetFiles = new();
        private readonly List<PartialFilePartList> TargetFileParts = new();

        public IList<string> GetSourceFiles() => SourceFiles.AsReadOnly();

        private readonly List<WeakReference<List<Tuple<int, int>>>> UsedSourceFileParts = new();

        public List<Tuple<int, int>> GetUsedSourceFileParts(int sourceFileIndex)
        {
            if (UsedSourceFileParts[sourceFileIndex].TryGetTarget(out var list))
                return list;

            UsedSourceFileParts[sourceFileIndex].SetTarget(list = new());
            for (var i = 0; i < TargetFileParts.Count; i++)
                for (var j = 0; j < TargetFileParts[i].Count; j++)
                    if (TargetFileParts[i][j].SourceIndex == sourceFileIndex)
                        list.Add(Tuple.Create(i, j));
            list.Sort((x, y) => { 
                return TargetFileParts[x.Item1][x.Item2].SourceOffset.CompareTo(TargetFileParts[y.Item1][y.Item2].SourceOffset);
            });
            return list;
        }

        public IList<string> GetFiles()
        {
            return TargetFiles.ToList();
        }

        public long GetFileSize(string file)
        {
            var targetFileIndex = TargetFiles.IndexOf(file);
            return TargetFileParts[targetFileIndex].FileSize;
        }

        private string NormalizePath(string path)
        {
            if (path == "")
                return path;
            path = path.Replace("\\", "/");
            while (path[0] == '/')
                path = path.Substring(1);
            return path;
        }

        private void ReassignTargetIndices()
        {
            for (int i = 0; i < TargetFiles.Count; i++)
            {
                for (var j = 0; j < TargetFileParts[i].Count; j++)
                {
                    var obj = TargetFileParts[i][j];
                    obj.TargetIndex = i;
                    TargetFileParts[i][j] = obj;
                }
            }
        }

        private void DeleteFile(string path, bool reassignTargetIndex = true)
        {
            path = NormalizePath(path);
            var targetFileIndex = TargetFiles.IndexOf(path);
            if (targetFileIndex == -1)
                return;

            TargetFiles.RemoveAt(targetFileIndex);
            TargetFileParts.RemoveAt(targetFileIndex);

            if (reassignTargetIndex)
                ReassignTargetIndices();
        }

        public int GetFileCount()
        {
            return TargetFiles.Count;
        }

        public int GetFileIndex(string targetFileName)
        {
            targetFileName = NormalizePath(targetFileName);
            var targetFileIndex = TargetFiles.IndexOf(targetFileName);
            if (targetFileIndex == -1)
            {
                TargetFiles.Add(targetFileName);
                TargetFileParts.Add(new());
                targetFileIndex = TargetFiles.Count - 1;
            }
            return targetFileIndex;
        }

        public PartialFilePartList GetFile(string targetFileName)
        {
            return TargetFileParts[GetFileIndex(targetFileName)];
        }

        public PartialFilePartList GetFile(int targetFileIndex)
        {
            return TargetFileParts[targetFileIndex];
        }

        public string GetFileRelativePath(int targetFileIndex)
        {
            return TargetFiles[targetFileIndex];
        }

        public void ApplyZiPatch(string patchFileName, ZiPatchFile patchFile)
        {
            var SourceIndex = SourceFiles.Count;
            SourceFiles.Add(patchFileName);
            UsedSourceFileParts.Add(new(null));
            foreach (var p in UsedSourceFileParts)
                p.SetTarget(null);

            var platform = ZiPatchConfig.PlatformId.Win32;
            foreach (var patchChunk in patchFile.GetChunks())
            {
                if (patchChunk is DeleteDirectoryChunk deleteDirectoryChunk)
                {
                    var prefix = deleteDirectoryChunk.DirName.ToLowerInvariant();
                    foreach (var fn in new List<string>(TargetFiles))
                    {
                        if (fn.ToLowerInvariant().StartsWith(prefix))
                            DeleteFile(fn, false);
                    }
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
                            var fileIndex = GetFileIndex(sqpkFile.TargetFile.RelativePath);
                            var file = TargetFileParts[fileIndex];
                            if (sqpkFile.FileOffset == 0)
                                file.Clear();

                            var offset = sqpkFile.FileOffset;
                            for (var i = 0; i < sqpkFile.CompressedData.Count; ++i)
                            {
                                var block = sqpkFile.CompressedData[i];
                                var dataOffset = (int)sqpkFile.CompressedDataSourceOffsets[i];
                                if (block.IsCompressed)
                                {
                                    file.Update(new PartialFilePart
                                    {
                                        TargetOffset = offset,
                                        TargetSize = block.DecompressedSize,
                                        TargetIndex = fileIndex,
                                        SourceIndex = SourceIndex,
                                        SourceOffset = dataOffset,
                                        IsDeflatedBlockData = true,
                                    });
                                }
                                else
                                {
                                    file.Update(new PartialFilePart
                                    {
                                        TargetOffset = offset,
                                        TargetSize = block.DecompressedSize,
                                        TargetIndex = fileIndex,
                                        SourceIndex = SourceIndex,
                                        SourceOffset = dataOffset,
                                    });
                                }
                                offset += block.DecompressedSize;
                            }

                            break;

                        case SqpkFile.OperationKind.RemoveAll:
                            var xpacPath = SqexFile.GetExpansionFolder((byte)sqpkFile.ExpansionId);

                            foreach (var fn in new List<string>(TargetFiles))
                            {
                                if (fn.ToLowerInvariant().StartsWith($"sqpack/{xpacPath}"))
                                    DeleteFile(fn, false);
                                else if (fn.ToLowerInvariant().StartsWith($"movie/{xpacPath}"))
                                    DeleteFile(fn, false);
                            }
                            ReassignTargetIndices();
                            break;

                        case SqpkFile.OperationKind.DeleteFile:
                            DeleteFile(sqpkFile.TargetFile.RelativePath);
                            break;
                    }
                }
                else if (patchChunk is SqpkAddData sqpkAddData)
                {
                    sqpkAddData.TargetFile.ResolvePath(platform);
                    var fileIndex = GetFileIndex(sqpkAddData.TargetFile.RelativePath);
                    var file = TargetFileParts[fileIndex];
                    file.Update(new PartialFilePart
                    {
                        TargetOffset = sqpkAddData.BlockOffset,
                        TargetSize = sqpkAddData.BlockNumber,
                        TargetIndex = fileIndex,
                        SourceIndex = SourceIndex,
                        SourceOffset = sqpkAddData.BlockDataSourceOffset,
                        Crc32OrPlaceholderEntryDataUnits = (uint)(sqpkAddData.BlockNumber >> 7) - 1,
                    });
                    file.Update(new PartialFilePart
                    {
                        TargetOffset = sqpkAddData.BlockOffset + sqpkAddData.BlockNumber,
                        TargetSize = sqpkAddData.BlockDeleteNumber,
                        TargetIndex = fileIndex,
                        SourceIndex = PartialFilePart.SourceIndex_Zeros,
                        Crc32OrPlaceholderEntryDataUnits = (uint)(sqpkAddData.BlockDeleteNumber >> 7) - 1,
                    });
                }
                else if (patchChunk is SqpkDeleteData sqpkDeleteData)
                {
                    sqpkDeleteData.TargetFile.ResolvePath(platform);
                    var fileIndex = GetFileIndex(sqpkDeleteData.TargetFile.RelativePath);
                    var file = TargetFileParts[fileIndex];
                    if (sqpkDeleteData.BlockNumber > 0)
                    {
                        file.Update(new PartialFilePart
                        {
                            TargetOffset = sqpkDeleteData.BlockOffset,
                            TargetSize = 1 << 7,
                            TargetIndex = fileIndex,
                            SourceIndex = PartialFilePart.SourceIndex_EmptyBlock,
                            Crc32OrPlaceholderEntryDataUnits = (uint)sqpkDeleteData.BlockNumber - 1,
                        });
                        if (sqpkDeleteData.BlockNumber > 1)
                        {
                            file.Update(new PartialFilePart
                            {
                                TargetOffset = sqpkDeleteData.BlockOffset + (1 << 7),
                                TargetSize = (sqpkDeleteData.BlockNumber - 1) << 7,
                                TargetIndex = fileIndex,
                                SourceIndex = PartialFilePart.SourceIndex_Zeros,
                            });
                        }
                    }
                }
                else if (patchChunk is SqpkExpandData sqpkExpandData)
                {
                    sqpkExpandData.TargetFile.ResolvePath(platform);
                    var fileIndex = GetFileIndex(sqpkExpandData.TargetFile.RelativePath);
                    var file = TargetFileParts[fileIndex];
                    if (sqpkExpandData.BlockNumber > 0)
                    {
                        file.Update(new PartialFilePart
                        {
                            TargetOffset = sqpkExpandData.BlockOffset,
                            TargetSize = 1 << 7,
                            TargetIndex = fileIndex,
                            SourceIndex = PartialFilePart.SourceIndex_EmptyBlock,
                            Crc32OrPlaceholderEntryDataUnits = (uint)sqpkExpandData.BlockNumber - 1,
                        });
                        if (sqpkExpandData.BlockNumber > 1)
                        {
                            file.Update(new PartialFilePart
                            {
                                TargetOffset = sqpkExpandData.BlockOffset + (1 << 7),
                                TargetSize = (sqpkExpandData.BlockNumber - 1) << 7,
                                TargetIndex = fileIndex,
                                SourceIndex = PartialFilePart.SourceIndex_Zeros,
                            });
                        }
                    }
                }
                else if (patchChunk is SqpkHeader sqpkHeader)
                {
                    sqpkHeader.TargetFile.ResolvePath(platform);
                    var fileIndex = GetFileIndex(sqpkHeader.TargetFile.RelativePath);
                    var file = TargetFileParts[fileIndex];
                    file.Update(new PartialFilePart
                    {
                        TargetOffset = sqpkHeader.HeaderKind == SqpkHeader.TargetHeaderKind.Version ? 0 : SqpkHeader.HEADER_SIZE,
                        TargetSize = SqpkHeader.HEADER_SIZE,
                        TargetIndex = fileIndex,
                        SourceIndex = SourceIndex,
                        SourceOffset = sqpkHeader.HeaderDataSourceOffset,
                    });
                }
            }
        }

        public void CalculateCrc32(List<Stream> sources)
        {
            foreach (var file in TargetFileParts)
                file.CalculateCrc32(sources);
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(SourceFiles.Count);
            foreach (var file in SourceFiles)
                writer.Write(file);
            writer.Write(TargetFiles.Count);
            for (var i = 0; i < TargetFiles.Count; i++)
            {
                writer.Write(TargetFiles[i]);
                var data = TargetFileParts[i].ToBytes();
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        public void ReadFrom(BinaryReader reader)
        {
            SourceFiles.Clear();
            UsedSourceFileParts.Clear();
            for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
            {
                SourceFiles.Add(reader.ReadString());
                UsedSourceFileParts.Add(new(null));
            }

            TargetFiles.Clear();
            TargetFileParts.Clear();
            for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
            {
                TargetFiles.Add(reader.ReadString());

                var dataLength = reader.ReadInt32();
                var data = new byte[dataLength];
                reader.Read(data, 0, dataLength);
                var parts = new PartialFilePartList();
                parts.FromBytes(data);
                TargetFileParts.Add(parts);
            }
        }
    }
}
