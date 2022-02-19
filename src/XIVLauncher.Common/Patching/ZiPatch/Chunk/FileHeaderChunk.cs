using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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


        public FileHeaderChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            Version = (byte)(reader.ReadUInt32() >> 16);
            PatchType = reader.ReadFixedLengthString(4u);
            EntryFiles = reader.ReadUInt32BE();

            if (Version == 3)
            {
                AddDirectories = reader.ReadUInt32BE();
                DeleteDirectories = reader.ReadUInt32BE();
                DeleteDataSize = reader.ReadUInt32BE() | ((long)reader.ReadUInt32BE() << 32);
                MinorVersion = reader.ReadUInt32BE();
                RepositoryName = reader.ReadUInt32BE();
                Commands = reader.ReadUInt32BE();
                SqpkAddCommands = reader.ReadUInt32BE();
                SqpkDeleteCommands = reader.ReadUInt32BE();
                SqpkExpandCommands = reader.ReadUInt32BE();
                SqpkHeaderCommands = reader.ReadUInt32BE();
                SqpkFileCommands = reader.ReadUInt32BE();
            }

            // 0xB8 of unknown data for V3, 0x08 of 0x00 for V2
            // ... Probably irrelevant.
            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override string ToString()
        {
            return $"{Type}:V{Version}:{RepositoryName}";
        }
    }
}