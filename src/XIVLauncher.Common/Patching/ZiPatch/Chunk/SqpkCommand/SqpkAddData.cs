using System.IO;

using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "A" (Add Data) command chunk.
    /// </summary>
    class SqpkAddData : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        public static new string Command = "A";

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
        /// Gets the block delete number.
        /// </summary>
        public int BlockDeleteNumber { get; protected set; }

        /// <summary>
        /// Gets the block data.
        /// </summary>
        public byte[] BlockData { get; protected set; }

        /// <summary>
        /// Gets the block data source offset.
        /// </summary>
        public long BlockDataSourceOffset { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkAddData"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public SqpkAddData(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            this.Reader.ReadBytes(3); // Alignment

            TargetFile = new SqpackDatFile(this.Reader);

            BlockOffset = this.Reader.ReadInt32BE() << 7;
            BlockNumber = this.Reader.ReadInt32BE() << 7;
            BlockDeleteNumber = this.Reader.ReadInt32BE() << 7;

            BlockDataSourceOffset = Offset + this.Reader.BaseStream.Position;
            BlockData = this.Reader.ReadBytes((int)BlockNumber);

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override void ApplyChunk(ZiPatchConfig config)
        {
            TargetFile.ResolvePath(config.Platform);

            var file = config.Store == null ?
                TargetFile.OpenStream(config.GamePath, FileMode.OpenOrCreate) :
                TargetFile.OpenStream(config.Store, config.GamePath, FileMode.OpenOrCreate);

            file.WriteFromOffset(BlockData, BlockOffset);
            file.Wipe(BlockDeleteNumber);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{Command}:{TargetFile}:{BlockOffset}:{BlockNumber}:{BlockDeleteNumber}";
        }
    }
}
