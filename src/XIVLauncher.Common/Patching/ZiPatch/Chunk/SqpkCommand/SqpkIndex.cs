using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "I" (Index) command chunk.
    /// </summary>
    class SqpkIndex : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        /// <remarks>
        /// This is a NOP on recent patcher versions.
        /// </remarks>
        public static new string Command = "I";

        /// <summary>
        /// Index command types.
        /// </summary>
        public enum IndexCommandKind : byte
        {
            /// <summary>
            /// Add index command.
            /// </summary>
            Add = (byte)'A',

            /// <summary>
            /// Delete index command.
            /// </summary>
            Delete = (byte)'D',
        }

        /// <summary>
        /// Gets the index command.
        /// </summary>
        public IndexCommandKind IndexCommand { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether this is a synonym.
        /// </summary>
        public bool IsSynonym { get; protected set; }

        /// <summary>
        /// Gets the target file.
        /// </summary>
        public SqpackIndexFile TargetFile { get; protected set; }

        /// <summary>
        /// Gets the file hash.
        /// </summary>
        public ulong FileHash { get; protected set; }

        /// <summary>
        /// Gets the block offset.
        /// </summary>
        public uint BlockOffset { get; protected set; }

        /// <summary>
        /// Gets the block number.
        /// </summary>
        // TODO: Figure out what this is used for
        public uint BlockNumber { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkIndex"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public SqpkIndex(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            IndexCommand = (IndexCommandKind)this.Reader.ReadByte();
            IsSynonym = this.Reader.ReadBoolean();
            this.Reader.ReadByte(); // Alignment

            TargetFile = new SqpackIndexFile(this.Reader);

            FileHash = this.Reader.ReadUInt64BE();

            BlockOffset = this.Reader.ReadUInt32BE();
            BlockNumber = this.Reader.ReadUInt32BE();

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{Command}:{IndexCommand}:{IsSynonym}:{TargetFile}:{FileHash:X8}:{BlockOffset}:{BlockNumber}";
        }
    }
}
