using System.IO;

using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// A "D" (Delete Data) command chunk.
    /// </summary>
    class SqpkDeleteData : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        public static new string Command = "D";

        /// <summary>
        /// Gets the target file.
        /// </summary>
        public SqpackDatFile TargetFile { get; protected set; }

        /// <summary>
        /// Gets the block offset.
        /// </summary>
        public int BlockOffset { get; protected set; }

        /// <summary>
        /// Gets the block number.
        /// </summary>
        public int BlockNumber { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkDeleteData"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public SqpkDeleteData(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            this.Reader.ReadBytes(3); // Alignment

            TargetFile = new SqpackDatFile(this.Reader);

            BlockOffset = this.Reader.ReadInt32BE() << 7;
            BlockNumber = this.Reader.ReadInt32BE();

            this.Reader.ReadUInt32(); // Reserved

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override void ApplyChunk(ZiPatchConfig config)
        {
            TargetFile.ResolvePath(config.Platform);

            var file = config.Store == null ?
                TargetFile.OpenStream(config.GamePath, FileMode.OpenOrCreate) :
                TargetFile.OpenStream(config.Store, config.GamePath, FileMode.OpenOrCreate);

            SqpackDatFile.WriteEmptyFileBlockAt(file, BlockOffset, BlockNumber);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{Command}:{TargetFile}:{BlockOffset}:{BlockNumber}";
        }
    }
}
