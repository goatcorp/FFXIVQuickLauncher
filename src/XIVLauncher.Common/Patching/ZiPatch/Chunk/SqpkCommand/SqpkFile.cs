using System.Collections.Generic;
using System.IO;
using System.Linq;

using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "F" (File) command chunk.
    /// </summary>
    internal class SqpkFile : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        public static new string Command = "F";

        /// <summary>
        /// File operation types.
        /// </summary>
        public enum OperationKind : byte
        {
            /// <summary>
            /// Add file.
            /// </summary>
            AddFile = (byte)'A',

            /// <summary>
            /// Remove all.
            /// </summary>
            RemoveAll = (byte)'R',

            // I've seen no cases in the wild of these two

            /// <summary>
            /// Delete file.
            /// </summary>
            DeleteFile = (byte)'D',

            /// <summary>
            /// Make directory tree.
            /// </summary>
            MakeDirTree = (byte)'M',
        }

        /// <summary>
        /// Gets the operation kind.
        /// </summary>
        public OperationKind Operation { get; private protected set; }

        /// <summary>
        /// Gets the file offset.
        /// </summary>
        public long FileOffset { get; private protected set; }

        /// <summary>
        /// Gets the file size.
        /// </summary>
        public ulong FileSize { get; private protected set; }

        /// <summary>
        /// Gets the expansion ID.
        /// </summary>
        public ushort ExpansionId { get; private protected set; }

        /// <summary>
        /// Gets the target file.
        /// </summary>
        public SqexFile TargetFile { get; private protected set; }

        /// <summary>
        /// Gets the compressed data source offsets.
        /// </summary>
        public List<long> CompressedDataSourceOffsets { get; private protected set; }

        /// <summary>
        /// Gets the compressed data.
        /// </summary>
        public List<SqpkCompressedBlock> CompressedData { get; private protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkFile"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public SqpkFile(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            Operation = (OperationKind)this.Reader.ReadByte();
            this.Reader.ReadBytes(2); // Alignment

            FileOffset = this.Reader.ReadInt64BE();
            FileSize = this.Reader.ReadUInt64BE();

            var pathLen = this.Reader.ReadUInt32BE();

            ExpansionId = this.Reader.ReadUInt16BE();
            this.Reader.ReadBytes(2);

            TargetFile = new SqexFile(this.Reader.ReadFixedLengthString(pathLen));

            if (Operation == OperationKind.AddFile)
            {
                CompressedDataSourceOffsets = new();
                CompressedData = new List<SqpkCompressedBlock>();

                while (Size - this.Reader.BaseStream.Position + start > 0)
                {
                    CompressedDataSourceOffsets.Add(Offset + this.Reader.BaseStream.Position);
                    CompressedData.Add(new SqpkCompressedBlock(this.Reader));
                    CompressedDataSourceOffsets[CompressedDataSourceOffsets.Count - 1] += CompressedData[CompressedData.Count - 1].HeaderSize;
                }
            }

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        private static bool RemoveAllFilter(string filePath) =>
            !new[] { ".var", "00000.bk2", "00001.bk2", "00002.bk2", "00003.bk2" }.Any(filePath.EndsWith);

        /// <inheritdoc/>
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
                        File.Delete(file);
                    break;

                case OperationKind.DeleteFile:
                    File.Delete(config.GamePath + "/" + TargetFile.RelativePath);
                    break;

                case OperationKind.MakeDirTree:
                    Directory.CreateDirectory(config.GamePath + "/" + TargetFile.RelativePath);
                    break;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{Command}:{Operation}:{FileOffset}:{FileSize}:{ExpansionId}:{TargetFile}";
        }
    }
}
