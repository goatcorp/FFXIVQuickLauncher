using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    /// <summary>
    /// An "APLY" (Apply Option) chunk.
    /// </summary>
    public class ApplyOptionChunk : ZiPatchChunk
    {
        /// <summary>
        /// The chunk type.
        /// </summary>
        public static new string Type = "APLY";

        /// <summary>
        /// Gets the ApplyOption kind.
        /// </summary>
        public enum ApplyOptionKind : uint
        {
            /// <summary>
            /// Ignore missing.
            /// </summary>
            IgnoreMissing = 1,

            /// <summary>
            /// Ignore old mismatch.
            /// </summary>
            IgnoreOldMismatch = 2,
        }

        /// <summary>
        /// Gets the option kind.
        /// </summary>
        public ApplyOptionKind OptionKind { get; protected set; }

        /// <summary>
        /// Gets the option value.
        /// </summary>
        /// <remarks>
        /// This is false on all files seen so far.
        /// </remarks>
        public bool OptionValue { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplyOptionChunk"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public ApplyOptionChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            OptionKind = (ApplyOptionKind)reader.ReadUInt32BE();

            // Discarded padding, always 0x0000_0004 as far as observed
            this.Reader.ReadBytes(4);

            var value = this.Reader.ReadUInt32BE() != 0;

            if (OptionKind == ApplyOptionKind.IgnoreMissing ||
                OptionKind == ApplyOptionKind.IgnoreOldMismatch)
                OptionValue = value;
            else
                OptionValue = false; // defaults to false if OptionKind isn't valid

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override void ApplyChunk(ZiPatchConfig config)
        {
            switch (OptionKind)
            {
                case ApplyOptionKind.IgnoreMissing:
                    config.IgnoreMissing = OptionValue;
                    break;

                case ApplyOptionKind.IgnoreOldMismatch:
                    config.IgnoreOldMismatch = OptionValue;
                    break;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{OptionKind}:{OptionValue}";
        }
    }
}
