using System.Collections.Generic;
using System.IO;
using System.Linq;
using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    internal class SqpkFile : SqpkChunk
    {
        public new static string Command = "F";

        public enum OperationKind : byte
        {
            AddFile = (byte)'A',
            RemoveAll = (byte)'R',

            // I've seen no cases in the wild of these two
            DeleteFile = (byte)'D',
            MakeDirTree = (byte)'M'
        }

        public OperationKind Operation { get; protected set; }
        public long FileOffset { get; protected set; }
        public long FileSize { get; protected set; }
        public ushort ExpansionId { get; protected set; }
        public SqexFile TargetFile { get; protected set; }

        public List<long> CompressedDataSourceOffsets { get; protected set; }
        public List<SqpkCompressedBlock> CompressedData { get; protected set; }

        public SqpkFile(BinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            using var advanceAfter = this.GetAdvanceOnDispose();
            Operation = (OperationKind)this.Reader.ReadByte();
            this.Reader.ReadBytes(2); // Alignment

            FileOffset = this.Reader.ReadInt64BE();
            FileSize = this.Reader.ReadInt64BE();

            var pathLen = this.Reader.ReadUInt32BE();

            ExpansionId = this.Reader.ReadUInt16BE();
            this.Reader.ReadBytes(2);

            TargetFile = new SqexFile(this.Reader.ReadFixedLengthString(pathLen));

            if (Operation == OperationKind.AddFile)
            {
                CompressedDataSourceOffsets = new();
                CompressedData = new List<SqpkCompressedBlock>();

                while (advanceAfter.NumBytesRemaining > 0)
                {
                    CompressedDataSourceOffsets.Add(Offset + this.Reader.BaseStream.Position);
                    CompressedData.Add(new SqpkCompressedBlock(this.Reader));
                    CompressedDataSourceOffsets[CompressedDataSourceOffsets.Count - 1] += CompressedData[CompressedData.Count - 1].HeaderSize;
                }
            }
        }

        private static bool RemoveAllFilter(string filePath) =>
            !new[] { ".var", "00000.bk2", "00001.bk2", "00002.bk2", "00003.bk2" }.Any(filePath.EndsWith);

        public override void ApplyChunk(ZiPatchConfig config)
        {
            switch (Operation)
            {
                // Default behaviour falls through to AddFile, though this shouldn't happen
                case OperationKind.AddFile:
                default:
                    // TODO: Check this. I *think* boot usually creates all the folders like sqpack, movie, etc., so this might be kind of a hack
                    TargetFile.CreateDirectoryTree(config.GamePath);

                    var fileStream = config.Store == null ? TargetFile.OpenStream(config.GamePath, FileMode.OpenOrCreate) : TargetFile.OpenStream(config.Store, config.GamePath, FileMode.OpenOrCreate);

                    if (FileOffset == 0)
                        fileStream.SetLength(0);

                    fileStream.Seek(FileOffset, SeekOrigin.Begin);
                    foreach (var block in CompressedData)
                        block.DecompressInto(fileStream);

                    break;

                case OperationKind.RemoveAll:
                    foreach (var file in SqexFile.GetAllExpansionFiles(config.GamePath, ExpansionId).Where(RemoveAllFilter))
                        new SqexFile(file).Delete(config.Store, config.GamePath);

                    break;

                case OperationKind.DeleteFile:
                    this.TargetFile.Delete(config.Store, config.GamePath);
                    break;

                case OperationKind.MakeDirTree:
                    Directory.CreateDirectory(config.GamePath + "/" + TargetFile.RelativePath);
                    break;
            }
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{Operation}:{FileOffset}:{FileSize}:{ExpansionId}:{TargetFile}";
        }
    }
}
