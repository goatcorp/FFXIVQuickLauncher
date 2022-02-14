using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;
using XIVLauncher.PatchInstaller.ZiPatch;
using XIVLauncher.PatchInstaller.ZiPatch.Chunk;
using XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public partial class PartialFileDef
    {
        private List<string> SourceFiles = new();
        private Dictionary<string, PartialFilePartList> FileParts = new();

        public IList<string> GetSourceFiles() => SourceFiles.AsReadOnly();

        public IList<string> GetFiles()
        {
            return FileParts.Keys.ToList();
        }

        public PartialFileViewStream GetFileStream(string file, List<Stream> sources)
        {
            return new PartialFileViewStream(sources, FileParts[file]);
        }

        public long GetFileSize(string file)
        {
            if (FileParts[file].Count == 0)
                return 0;
            return FileParts[file][FileParts[file].Count - 1].TargetEnd;
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

        public PartialFilePartList GetFile(string targetFileName)
        {
            targetFileName = NormalizePath(targetFileName);
            try
            {
                return FileParts[targetFileName];
            }
            catch (KeyNotFoundException)
            {
                return FileParts[targetFileName] = new PartialFilePartList();
            }
        }

        public void ApplyZiPatch(string patchFileName, ZiPatchFile patchFile)
        {
            var SourceIndex = SourceFiles.Count;
            SourceFiles.Add(patchFileName);
            var platform = ZiPatchConfig.PlatformId.Win32;
            foreach (var patchChunk in patchFile.GetChunks())
            {
                if (patchChunk is DeleteDirectoryChunk deleteDirectoryChunk)
                {
                    FileParts = FileParts
                        .Where(x => !x.Key.ToLowerInvariant().StartsWith(deleteDirectoryChunk.DirName.ToLowerInvariant()))
                        .ToDictionary(x => x.Key, x => x.Value);
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
                            var file = GetFile(sqpkFile.TargetFile.RelativePath);
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
                                        SourceIndex = SourceIndex,
                                        SourceOffset = dataOffset,
                                        SourceSize = block.CompressedSize,
                                        SourceIsDeflated = true,
                                    });
                                }
                                else
                                {
                                    file.Update(new PartialFilePart
                                    {
                                        TargetOffset = offset,
                                        TargetSize = block.DecompressedSize,
                                        SourceIndex = SourceIndex,
                                        SourceOffset = dataOffset,
                                        SourceSize = block.DecompressedSize,
                                    });
                                }
                                offset += block.DecompressedSize;
                            }

                            break;

                        case SqpkFile.OperationKind.RemoveAll:
                            var xpacPath = SqexFile.GetExpansionFolder((byte)sqpkFile.ExpansionId);

                            FileParts = FileParts
                                .Where(x => !x.Key.ToLowerInvariant().StartsWith($"sqpack/{xpacPath}"))
                                .Where(x => !x.Key.ToLowerInvariant().StartsWith($"movie/{xpacPath}"))
                                .ToDictionary(x => x.Key, x => x.Value);
                            break;

                        case SqpkFile.OperationKind.DeleteFile:
                            FileParts.Remove(NormalizePath(sqpkFile.TargetFile.RelativePath));
                            break;
                    }
                }
                else if (patchChunk is SqpkAddData sqpkAddData)
                {
                    sqpkAddData.TargetFile.ResolvePath(platform);
                    GetFile(sqpkAddData.TargetFile.RelativePath).Update(new PartialFilePart
                    {
                        TargetOffset = sqpkAddData.BlockOffset,
                        TargetSize = sqpkAddData.BlockNumber,
                        SourceIndex = SourceIndex,
                        SourceOffset = sqpkAddData.BlockDataSourceOffset,
                        SourceSize = sqpkAddData.BlockNumber,
                    });
                    GetFile(sqpkAddData.TargetFile.RelativePath).Update(new PartialFilePart
                    {
                        TargetOffset = sqpkAddData.BlockOffset + sqpkAddData.BlockNumber,
                        TargetSize = sqpkAddData.BlockDeleteNumber,
                        SourceIndex = PartialFilePart.SourceIndex_Zeros,
                        SourceSize = sqpkAddData.BlockDeleteNumber,
                    });
                }
                else if (patchChunk is SqpkDeleteData sqpkDeleteData)
                {
                    sqpkDeleteData.TargetFile.ResolvePath(platform);
                    GetFile(sqpkDeleteData.TargetFile.RelativePath).Update(new PartialFilePart
                    {
                        TargetOffset = sqpkDeleteData.BlockOffset,
                        TargetSize = sqpkDeleteData.BlockNumber << 7,
                        SourceIndex = PartialFilePart.SourceIndex_EmptyBlock,
                        SourceSize = sqpkDeleteData.BlockNumber << 7,
                    });
                }
                else if (patchChunk is SqpkExpandData sqpkExpandData)
                {
                    sqpkExpandData.TargetFile.ResolvePath(platform);
                    GetFile(sqpkExpandData.TargetFile.RelativePath).Update(new PartialFilePart
                    {
                        TargetOffset = sqpkExpandData.BlockOffset,
                        TargetSize = sqpkExpandData.BlockNumber << 7,
                        SourceIndex = PartialFilePart.SourceIndex_EmptyBlock,
                        SourceSize = sqpkExpandData.BlockNumber << 7,
                    });
                }
                else if (patchChunk is SqpkHeader sqpkHeader)
                {
                    sqpkHeader.TargetFile.ResolvePath(platform);
                    GetFile(sqpkHeader.TargetFile.RelativePath).Update(new PartialFilePart
                    {
                        TargetOffset = sqpkHeader.HeaderKind == SqpkHeader.TargetHeaderKind.Version ? 0 : SqpkHeader.HEADER_SIZE,
                        TargetSize = SqpkHeader.HEADER_SIZE,
                        SourceIndex = SourceIndex,
                        SourceOffset = sqpkHeader.HeaderDataSourceOffset,
                        SourceSize = SqpkHeader.HEADER_SIZE,
                    });
                }
            }
        }

        public void CalculateCrc32(List<Stream> sources)
        {
            foreach (var file in FileParts)
                file.Value.CalculateCrc32(new PartialFileViewStream(sources, file.Value));
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(SourceFiles.Count);
            foreach (var file in SourceFiles)
                writer.Write(file);
            writer.Write(FileParts.Count);
            foreach(var file in FileParts)
            {
                writer.Write(file.Key);
                var data = file.Value.ToBytes();
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        public void ReadFrom(BinaryReader reader)
        {
            SourceFiles.Clear();
            for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
                SourceFiles.Add(reader.ReadString());

            FileParts.Clear();
            for (int i = 0, i_ = reader.ReadInt32(); i < i_; i++)
            {
                var key = reader.ReadString();
                var dataLength = reader.ReadInt32();
                var data = new byte[dataLength];
                reader.Read(data, 0, dataLength);
                FileParts[key] = new PartialFilePartList();
                FileParts[key].FromBytes(data);
            }
        }
    }
}
