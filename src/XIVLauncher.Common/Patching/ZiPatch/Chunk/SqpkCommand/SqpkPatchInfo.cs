using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "X" (Patch Info) command chunk.
    /// </summary>
    internal class SqpkPatchInfo : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        /// <remarks>
        /// This is a NOP on recent patcher versions.
        /// </remarks>
        public static new string Command = "X";

        // Don't know what this stuff is for

        /// <summary>
        /// Gets the status.
        /// </summary>
        public byte Status { get; protected set; }

        /// <summary>
        /// Gets the version.
        /// </summary>
        public byte Version { get; protected set; }

        /// <summary>
        /// Gets the install size.
        /// </summary>
        public ulong InstallSize { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkPatchInfo"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public SqpkPatchInfo(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            Status = this.Reader.ReadByte();
            Version = this.Reader.ReadByte();
            this.Reader.ReadByte(); // Alignment

            InstallSize = this.Reader.ReadUInt64BE();

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{Command}:{Status}:{Version}:{InstallSize}";
        }
    }
}
