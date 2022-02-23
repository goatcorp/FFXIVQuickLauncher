using System.IO;
using System.Text;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    /// <summary>
    /// An SQPack dat file.
    /// </summary>
    class SqpackDatFile : SqpackFile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqpackDatFile"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        public SqpackDatFile(BinaryReader reader) : base(reader) { }

        /// <summary>
        /// Get a platform dependent filename.
        /// </summary>
        /// <param name="platform">Platform kind.</param>
        /// <returns>The filename.</returns>
        protected override string GetFileName(ZiPatchConfig.PlatformId platform) =>
            $"{base.GetFileName(platform)}.dat{FileId}";

        /// <summary>
        /// Write an empty file block at a given strema position.
        /// </summary>
        /// <param name="stream">Stream to write.</param>
        /// <param name="offset">Stream offset.</param>
        /// <param name="blockNumber">Block number.</param>
        public static void WriteEmptyFileBlockAt(SqexFileStream stream, int offset, int blockNumber)
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
