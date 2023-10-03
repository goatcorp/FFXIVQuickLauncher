using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    // ReSharper disable once InconsistentNaming
    public class XXXXChunk : ZiPatchChunk
    {
        // TODO: This... Never happens.
        public new static string Type = "XXXX";

        protected override void ReadChunk()
        {
            using var advanceAfter = new AdvanceOnDispose(this.Reader, Size);
        }

        public XXXXChunk(ChecksumBinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        public override string ToString()
        {
            return Type;
        }
    }
}