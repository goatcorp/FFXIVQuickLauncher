using System.IO;
using System.Text;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    class SqpackDatFile : SqpackFile
    {
        public SqpackDatFile(BinaryReader reader) : base(reader) {}


        protected override string GetFileName(ZiPatchConfig.PlatformId platform) =>
            $"{base.GetFileName(platform)}.dat{FileId}";


        public static void WriteEmptyFileBlockAt(SqexFileStream stream, long offset, long blockNumber)
        {
            stream.WipeFromOffset(blockNumber << 7, offset);
            stream.Position = offset;

            using (var file = new BinaryWriter(stream, Encoding.Default, true))
            {
                // FileBlockHeader - the 0 writes are technically unnecessary but are in for illustrative purposes

                // Block size
                file.Write(1 << 7);
                // ????
                file.Write(0);
                // File size
                file.Write(0);
                // Total number of blocks?
                file.Write(blockNumber - 1);
                // Used number of blocks?
                file.Write(0);
            }
        }
    }
}