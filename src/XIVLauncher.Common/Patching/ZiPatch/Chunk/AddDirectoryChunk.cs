using System.IO;
using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    public class AddDirectoryChunk : ZiPatchChunk
    {
        public new static string Type = "ADIR";

        public string DirName { get; protected set; }

        protected override void ReadChunk()
        {
            using var advanceAfter = new AdvanceOnDispose(this.Reader, Size);
            var dirNameLen = this.Reader.ReadUInt32BE();

            DirName = this.Reader.ReadFixedLengthString(dirNameLen);
        }


        public AddDirectoryChunk(ChecksumBinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        public override void ApplyChunk(ZiPatchConfig config)
        {
            Directory.CreateDirectory(config.GamePath + DirName);
        }

        public override string ToString()
        {
            return $"{Type}:{DirName}";
        }
    }
}