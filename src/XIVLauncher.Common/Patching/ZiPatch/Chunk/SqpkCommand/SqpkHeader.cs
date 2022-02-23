using System.IO;

using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "H" (Header) command chunk.
    /// </summary>
    class SqpkHeader : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        public static new string Command = "H";

        /// <summary>
        /// Target file types.
        /// </summary>
        public enum TargetFileKind : byte
        {
            /// <summary>
            /// Dat file.
            /// </summary>
            Dat = (byte)'D',

            /// <summary>
            /// Index file.
            /// </summary>
            Index = (byte)'I',
        }

        /// <summary>
        /// Target header types.
        /// </summary>
        public enum TargetHeaderKind : byte
        {
            /// <summary>
            /// Version header.
            /// </summary>
            Version = (byte)'V',

            /// <summary>
            /// Index header.
            /// </summary>
            Index = (byte)'I',

            /// <summary>
            /// Data header.
            /// </summary>
            Data = (byte)'D',
        }

        public const int HEADER_SIZE = 1024;

        /// <summary>
        /// Gets the file kind.
        /// </summary>
        public TargetFileKind FileKind { get; protected set; }

        /// <summary>
        /// Gets the header kind.
        /// </summary>
        public TargetHeaderKind HeaderKind { get; protected set; }

        /// <summary>
        /// Gets the target file.
        /// </summary>
        public SqpackFile TargetFile { get; protected set; }

        /// <summary>
        /// Gets the header data.
        /// </summary>
        public byte[] HeaderData { get; protected set; }

        /// <summary>
        /// Gets the header data source offset.
        /// </summary>
        public long HeaderDataSourceOffset { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkHeader"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public SqpkHeader(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            FileKind = (TargetFileKind)this.Reader.ReadByte();
            HeaderKind = (TargetHeaderKind)this.Reader.ReadByte();
            this.Reader.ReadByte(); // Alignment

            if (FileKind == TargetFileKind.Dat)
                TargetFile = new SqpackDatFile(this.Reader);
            else
                TargetFile = new SqpackIndexFile(this.Reader);

            HeaderDataSourceOffset = Offset + this.Reader.BaseStream.Position;
            HeaderData = this.Reader.ReadBytes(HEADER_SIZE);

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override void ApplyChunk(ZiPatchConfig config)
        {
            TargetFile.ResolvePath(config.Platform);

            var file = config.Store == null ?
                TargetFile.OpenStream(config.GamePath, FileMode.OpenOrCreate) :
                TargetFile.OpenStream(config.Store, config.GamePath, FileMode.OpenOrCreate);

            file.WriteFromOffset(HeaderData, HeaderKind == TargetHeaderKind.Version ? 0 : HEADER_SIZE);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:{Command}:{FileKind}:{HeaderKind}:{TargetFile}";
        }
    }
}
