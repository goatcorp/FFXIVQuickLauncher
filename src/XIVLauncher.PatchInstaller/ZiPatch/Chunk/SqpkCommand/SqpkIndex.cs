using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "I" (Index) command chunk.
    /// </summary>
    class SqpkIndex : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        // This is a NOP on recent patcher versions.
        public new static string Command = "I";

        /// <summary>
        /// Index command kinds.
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

        public IndexCommandKind IndexCommand { get; protected set; }
        public bool IsSynonym { get; protected set; }
        public SqpackIndexFile TargetFile { get; protected set; }
        public ulong FileHash { get; protected set; }
        public uint BlockOffset { get; protected set; }

        // TODO: Figure out what this is used for
        public uint BlockNumber { get; protected set; }



        public SqpkIndex(ChecksumBinaryReader reader, int size) : base(reader, size) {}

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            IndexCommand = (IndexCommandKind)reader.ReadByte();
            IsSynonym = reader.ReadBoolean();
            reader.ReadByte(); // Alignment

            TargetFile = new SqpackIndexFile(reader);

            FileHash = reader.ReadUInt64BE();

            BlockOffset = reader.ReadUInt32BE();
            BlockNumber = reader.ReadUInt32BE();

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{IndexCommand}:{IsSynonym}:{TargetFile}:{FileHash:X8}:{BlockOffset}:{BlockNumber}";
        }
    }
}
