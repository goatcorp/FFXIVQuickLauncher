using System.IO;
using System.IO.Compression;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    /// <summary>
    /// An SQPack compressed block.
    /// </summary>
    class SqpkCompressedBlock
    {
        /// <summary>
        /// Gets the header size.
        /// </summary>
        public int HeaderSize { get; protected set; }

        /// <summary>
        /// Gets the compressed size.
        /// </summary>
        public int CompressedSize { get; protected set; }

        /// <summary>
        /// Gets the decompressed size.
        /// </summary>
        public int DecompressedSize { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the data is compressed.
        /// </summary>
        public bool IsCompressed => CompressedSize != 0x7d00;

        /// <summary>
        /// Gets the compressed block length.
        /// </summary>
        public int CompressedBlockLength => (int)(((IsCompressed ? CompressedSize : DecompressedSize) + 143) & 0xFFFF_FF80);

        /// <summary>
        /// Gets the compressed block.
        /// </summary>
        public byte[] CompressedBlock { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkCompressedBlock"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        public SqpkCompressedBlock(BinaryReader reader)
        {
            HeaderSize = reader.ReadInt32();
            reader.ReadUInt32(); // Pad

            CompressedSize = reader.ReadInt32();
            DecompressedSize = reader.ReadInt32();

            if (IsCompressed)
                CompressedBlock = reader.ReadBytes(CompressedBlockLength - HeaderSize);
            else
            {
                CompressedBlock = reader.ReadBytes(DecompressedSize);

                reader.ReadBytes(CompressedBlockLength - HeaderSize - DecompressedSize);
            }
        }

        /// <summary>
        /// Decompress the reader into another stream.
        /// </summary>
        /// <param name="outStream">Output stream.</param>
        public void DecompressInto(Stream outStream)
        {
            if (IsCompressed)
                using (var stream = new DeflateStream(new MemoryStream(CompressedBlock), CompressionMode.Decompress))
                    stream.CopyTo(outStream);
            else
                using (var stream = new MemoryStream(CompressedBlock))
                    stream.CopyTo(outStream);
        }
    }
}
