using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    internal class SqpkPatchInfo : SqpkChunk
    {
        // This is a NOP on recent patcher versions
        public new static string Command = "X";

        // Don't know what this stuff is for
        public byte Status { get; protected set; }
        public byte Version { get; protected set; }
        public ulong InstallSize { get; protected set; }

        public SqpkPatchInfo(ChecksumBinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            using var advanceAfter = new AdvanceOnDispose(this.Reader, Size);
            Status = this.Reader.ReadByte();
            Version = this.Reader.ReadByte();
            this.Reader.ReadByte(); // Alignment

            InstallSize = this.Reader.ReadUInt64BE();
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{Status}:{Version}:{InstallSize}";
        }
    }
}