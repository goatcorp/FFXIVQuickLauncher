using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    /// <summary>
    /// An "FHDR" (File Header) chunk.
    /// </summary>
    public class FileHeaderChunk : ZiPatchChunk
    {
        /// <summary>
        /// The chunk type.
        /// </summary>
        public static new string Type = "FHDR";

        // V1?/2

        /// <summary>
        /// Gets the version.
        /// </summary>
        public byte Version { get; protected set; }

        /// <summary>
        /// Gets the patch type.
        /// </summary>
        public string PatchType { get; protected set; }

        /// <summary>
        /// Gets the number of files.
        /// </summary>
        public uint EntryFiles { get; protected set; }

        // V3

        /// <summary>
        /// Gets the number of directories to add.
        /// </summary>
        public uint AddDirectories { get; protected set; }

        /// <summary>
        /// Gets the number of directories to delete.
        /// </summary>
        public uint DeleteDirectories { get; protected set; }

        /// <summary>
        /// Gets the size of data to delete.
        /// </summary>
        public long DeleteDataSize { get; protected set; } // Split in 2 DWORD; Low, High

        /// <summary>
        /// Gets the minor version.
        /// </summary>
        public uint MinorVersion { get; protected set; }

        /// <summary>
        /// Gets the repository name.
        /// </summary>
        public uint RepositoryName { get; protected set; }

        /// <summary>
        /// Gets the commands.
        /// </summary>
        public uint Commands { get; protected set; }

        /// <summary>
        /// Gets the nummber of Add commands.
        /// </summary>
        public uint SqpkAddCommands { get; protected set; }

        /// <summary>
        /// Gets the number of Delete commands.
        /// </summary>
        public uint SqpkDeleteCommands { get; protected set; }

        /// <summary>
        /// Gets the number of Expand commands.
        /// </summary>
        public uint SqpkExpandCommands { get; protected set; }

        /// <summary>
        /// Gets the number of Header commands.
        /// </summary>
        public uint SqpkHeaderCommands { get; protected set; }

        /// <summary>
        /// Gets the number of file commands.
        /// </summary>
        public uint SqpkFileCommands { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileHeaderChunk"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        public FileHeaderChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) { }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            Version = (byte)(this.Reader.ReadUInt32() >> 16);
            PatchType = this.Reader.ReadFixedLengthString(4u);
            EntryFiles = this.Reader.ReadUInt32BE();

            if (Version == 3)
            {
                AddDirectories = this.Reader.ReadUInt32BE();
                DeleteDirectories = this.Reader.ReadUInt32BE();
                DeleteDataSize = this.Reader.ReadUInt32BE() | ((long)this.Reader.ReadUInt32BE() << 32);
                MinorVersion = this.Reader.ReadUInt32BE();
                RepositoryName = this.Reader.ReadUInt32BE();
                Commands = this.Reader.ReadUInt32BE();
                SqpkAddCommands = this.Reader.ReadUInt32BE();
                SqpkDeleteCommands = this.Reader.ReadUInt32BE();
                SqpkExpandCommands = this.Reader.ReadUInt32BE();
                SqpkHeaderCommands = this.Reader.ReadUInt32BE();
                SqpkFileCommands = this.Reader.ReadUInt32BE();
            }

            // 0xB8 of unknown data for V3, 0x08 of 0x00 for V2
            // ... Probably irrelevant.
            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Type}:V{Version}:{RepositoryName}";
        }
    }
}
