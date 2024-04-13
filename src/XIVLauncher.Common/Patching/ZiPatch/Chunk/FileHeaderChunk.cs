using System.IO;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    public class FileHeaderChunk : ZiPatchChunk
    {
        public new static string Type = "FHDR";

        // V1?/2
        public byte Version { get; protected set; }
        public string PatchType { get; protected set; }
        public uint EntryFiles { get; protected set; }

        // V3
        public uint AddDirectories { get; protected set; }
        public uint DeleteDirectories { get; protected set; }
        public long DeleteDataSize { get; protected set; } // Split in 2 DWORD; Low, High
        public uint MinorVersion { get; protected set; }
        public uint RepositoryName { get; protected set; }
        public uint Commands { get; protected set; }
        public uint SqpkAddCommands { get; protected set; }
        public uint SqpkDeleteCommands { get; protected set; }
        public uint SqpkExpandCommands { get; protected set; }
        public uint SqpkHeaderCommands { get; protected set; }
        public uint SqpkFileCommands { get; protected set; }

        public FileHeaderChunk(BinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            using var advanceAfter = this.GetAdvanceOnDispose();

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
        }

        public override string ToString()
        {
            return $"{Type}:V{Version}:{RepositoryName}";
        }
    }
}
