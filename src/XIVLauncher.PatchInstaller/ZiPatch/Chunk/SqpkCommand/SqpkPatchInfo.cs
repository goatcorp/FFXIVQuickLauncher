using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand
{
    /// <summary>
    /// An "X" (Patch Info) command chunk.
    /// </summary>
    class SqpkPatchInfo : SqpkChunk
    {
        /// <summary>
        /// Gets the command type.
        /// </summary>
        // This is a NOP on recent patcher versions
        public new static string Command = "X";


        // Don't know what this stuff is for
        public byte Status { get; protected set; }
        public byte Version { get; protected set; }
        public ulong InstallSize { get; protected set; }


        public SqpkPatchInfo(ChecksumBinaryReader reader, int size) : base(reader, size) {}

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            Status = reader.ReadByte();
            Version = reader.ReadByte();
            reader.ReadByte(); // Alignment

            InstallSize = reader.ReadUInt64BE();

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{Status}:{Version}:{InstallSize}";
        }
    }
}
