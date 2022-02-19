using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    class SqpkPatchInfo : SqpkChunk
    {
        // This is a NOP on recent patcher versions
        public new static string Command = "X";


        // Don't know what this stuff is for
        public byte Status { get; protected set; }
        public byte Version { get; protected set; }
        public ulong InstallSize { get; protected set; }


        public SqpkPatchInfo(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) {}

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