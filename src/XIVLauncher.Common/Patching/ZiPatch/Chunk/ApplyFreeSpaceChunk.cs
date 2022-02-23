using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    /// <summary>
    /// An "APFS" (Apply Free Space) chunk.
    /// </summary>
    /// <remarks>
    /// This is a NOP on recent patcher versions, so I don't think we'll be seeing it.
    /// </remarks>
    public class ApplyFreeSpaceChunk : ZiPatchChunk
    {
        /// <summary>
        /// The chunk type.
        /// </summary>
        public static new string Type = "APFS";

        /// <summary>
        /// Gets theoretical unknown field A.
        /// </summary>
        // TODO: No samples of this were found, so this field is theoretical
        public long UnknownFieldA { get; protected set; }

        /// <summary>
        /// Gets theoretical unknown field B.
        /// </summary>
        // TODO: No samples of this were found, so this field is theoretical
        public long UnknownFieldB { get; protected set; }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            UnknownFieldA = this.Reader.ReadInt64BE();
            UnknownFieldB = this.Reader.ReadInt64BE();

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplyFreeSpaceChunk"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public ApplyFreeSpaceChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{UnknownFieldA}:{UnknownFieldB}";
        }
    }
}
