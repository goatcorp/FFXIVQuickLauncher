using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.Util
{
    /// <summary>
    /// A binary reader with a rolling CRC32.
    /// </summary>
    public class ChecksumBinaryReader : BinaryReader
    {
        private readonly Crc32 _crc32 = new Crc32();

        /// <summary>
        /// Initializes a new instance of the <see cref="ChecksumBinaryReader"/> class.
        /// </summary>
        /// <param name="input">Input stream.</param>
        public ChecksumBinaryReader(Stream input) : base(input) {}

        /// <summary>
        /// Initialize the CRC32 counter.
        /// </summary>
        public void InitCrc32()
        {
            _crc32.Init();
        }

        /// <summary>
        /// Get the current CRC32.
        /// </summary>
        /// <returns>CRC32.</returns>
        public uint GetCrc32()
        {
            return _crc32.Checksum;
        }

        /// <summary>
        /// Read from the underlying stream.
        /// </summary>
        /// <param name="count">Number of bytes to read.</param>
        /// <returns>The bytes that were read.</returns>
        public override byte[] ReadBytes(int count)
        {
            var result = base.ReadBytes(count);

            _crc32.Update(result);

            return result;
        }

        /// <summary>
        /// Read a single byte from the underlying stream.
        /// </summary>
        /// <returns>The read byte.</returns>
        public override byte ReadByte()
        {
            var result = base.ReadByte();

            _crc32.Update(result);

            return result;
        }

        /// <inheritdoc/>
        public override sbyte ReadSByte() => (sbyte)ReadByte();

        /// <inheritdoc/>
        public override bool ReadBoolean() => ReadByte() != 0;

        /// <inheritdoc/>
        public override char ReadChar() => (char)ReadByte();

        /// <inheritdoc/>
        public override short ReadInt16() => BitConverter.ToInt16(ReadBytes(sizeof(short)), 0);

        /// <inheritdoc/>
        public override ushort ReadUInt16() => BitConverter.ToUInt16(ReadBytes(sizeof(ushort)), 0);

        /// <inheritdoc/>
        public override int ReadInt32() => BitConverter.ToInt32(ReadBytes(sizeof(int)), 0);

        /// <inheritdoc/>
        public override uint ReadUInt32() => BitConverter.ToUInt32(ReadBytes(sizeof(uint)), 0);

        /// <inheritdoc/>
        public override long ReadInt64() => BitConverter.ToInt64(ReadBytes(sizeof(long)), 0);

        /// <inheritdoc/>
        public override ulong ReadUInt64() => BitConverter.ToUInt64(ReadBytes(sizeof(ulong)), 0);

        /// <inheritdoc/>
        public override float ReadSingle() => BitConverter.ToSingle(ReadBytes(sizeof(float)), 0);

        /// <inheritdoc/>
        public override double ReadDouble() => BitConverter.ToDouble(ReadBytes(sizeof(float)), 0);
    }
}
