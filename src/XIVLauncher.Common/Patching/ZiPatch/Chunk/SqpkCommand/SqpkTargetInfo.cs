using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// A "T" (Target Info) command chunk.
    /// </summary>
    internal class SqpkTargetInfo : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        // Only Platform is used on recent patcher versions
        public static new string Command = "T";

        /// <summary>
        /// Region ID types.
        /// </summary>
        // US/EU/JP/ZH are Global
        // KR is unknown
        public enum RegionId : short
        {
            /// <summary>
            /// Global.
            /// </summary>
            Global = -1,
        }

        /// <summary>
        /// Gets the platform.
        /// </summary>
        public ZiPatchConfig.PlatformId Platform { get; protected set; }

        /// <summary>
        /// Gets the region ID.
        /// </summary>
        public RegionId Region { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether debug is enabled.
        /// </summary>
        public bool IsDebug { get; protected set; }

        /// <summary>
        /// Gets the version.
        /// </summary>
        public ushort Version { get; protected set; }

        /// <summary>
        /// Gets the deleted data size.
        /// </summary>
        public ulong DeletedDataSize { get; protected set; }

        /// <summary>
        /// Gets the seek count.
        /// </summary>
        public ulong SeekCount { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkTargetInfo"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public SqpkTargetInfo(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            // Reserved
            this.Reader.ReadBytes(3);

            Platform = (ZiPatchConfig.PlatformId)this.Reader.ReadUInt16BE();
            Region = (RegionId)this.Reader.ReadInt16BE();
            IsDebug = this.Reader.ReadInt16BE() != 0;
            Version = this.Reader.ReadUInt16BE();
            DeletedDataSize = this.Reader.ReadUInt64();
            SeekCount = this.Reader.ReadUInt64();

            // Empty 32 + 64 bytes
            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override void ApplyChunk(ZiPatchConfig config)
        {
            config.Platform = Platform;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{Command}:{Platform}:{Region}:{IsDebug}:{Version}:{DeletedDataSize}:{SeekCount}";
        }
    }
}
