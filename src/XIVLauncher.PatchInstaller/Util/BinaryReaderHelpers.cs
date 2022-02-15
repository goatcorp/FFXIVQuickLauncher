using System;
using System.IO;
using System.Text;
using Serilog;

namespace XIVLauncher.PatchInstaller.Util
{
    /// <summary>
    /// Extension methods for <see cref="BinaryReader"/> and <see cref="byte"/> arrays.
    /// </summary>
    // https://stackoverflow.com/a/15274591
    static class BinaryReaderHelpers
    {
        /// <summary>
        /// Reads a fixed length ASCII encoded string from the current stream and advances the current position of the stream by that many bytes.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="length">String length.</param>
        /// <returns>A fixed length ASCII encoded string.</returns>
        public static string ReadFixedLengthString(this BinaryReader reader, uint length)
        {
            return Encoding.ASCII.GetString(reader.ReadBytesRequired((int)length)).TrimEnd((char)0);
        }

        /// <summary>
        /// Reverse the order of the bytes, in-place.
        /// </summary>
        /// <param name="b">Byte array.</param>
        /// <returns>A reference to the same array.</returns>
        /// <remarks>This MODIFIIES THE GIVEN ARRAY and returns itself.</remarks>
        public static byte[] Reverse(this byte[] b)
        {
            Array.Reverse(b);
            return b;
        }

        /// <summary>
        /// Reads a 2-byte unsigned big-endian integer from the current stream and advances the current position of the stream by 2 bytes.
        /// </summary>
        /// <param name="binRdr">Binary reader.</param>
        /// <returns> A 2-byte unsigned big-endian integer read from the current stream.</returns>
        public static UInt16 ReadUInt16BE(this BinaryReader binRdr)
        {
            return BitConverter.ToUInt16(binRdr.ReadBytesRequired(sizeof(UInt16)).Reverse(), 0);
        }

        /// <summary>
        /// Reads a 2-byte signed big-endian integer from the current stream and advances the current position of the stream by 2 bytes.
        /// </summary>
        /// <param name="binRdr">Binary reader.</param>
        /// <returns> A 2-byte signed big-endian integer read from the current stream.</returns>
        public static Int16 ReadInt16BE(this BinaryReader binRdr)
        {
            return BitConverter.ToInt16(binRdr.ReadBytesRequired(sizeof(Int16)).Reverse(), 0);
        }

        /// <summary>
        /// Reads a 4-byte unsigned big-endian integer from the current stream and advances the current position of the stream by 4 bytes.
        /// </summary>
        /// <param name="binRdr">Binary reader.</param>
        /// <returns> A 4-byte unsigned big-endian integer read from the current stream.</returns>
        public static UInt32 ReadUInt32BE(this BinaryReader binRdr)
        {
            return BitConverter.ToUInt32(binRdr.ReadBytesRequired(sizeof(UInt32)).Reverse(), 0);
        }

        /// <summary>
        /// Reads a 4-byte signed big-endian integer from the current stream and advances the current position of the stream by 4 bytes.
        /// </summary>
        /// <param name="binRdr">Binary reader.</param>
        /// <returns> A 4-byte signed big-endian integer read from the current stream.</returns>
        public static Int32 ReadInt32BE(this BinaryReader binRdr)
        {
            return BitConverter.ToInt32(binRdr.ReadBytesRequired(sizeof(Int32)).Reverse(), 0);
        }

        /// <summary>
        /// Reads an 8-byte unsigned big-endian integer from the current stream and advances the current position of the stream by 8 bytes.
        /// </summary>
        /// <param name="binRdr">Binary reader.</param>
        /// <returns> An 8-byte unsigned big-endian integer read from the current stream.</returns>
        public static UInt64 ReadUInt64BE(this BinaryReader binRdr)
        {
            return BitConverter.ToUInt64(binRdr.ReadBytesRequired(sizeof(UInt64)).Reverse(), 0);
        }

        /// <summary>
        /// Reads an 8-byte signed big-endian integer from the current stream and advances the current position of the stream by 8 bytes.
        /// </summary>
        /// <param name="binRdr">Binary reader.</param>
        /// <returns> An 8-byte signed big-endian integer read from the current stream.</returns>
        public static Int64 ReadInt64BE(this BinaryReader binRdr)
        {
            return BitConverter.ToInt64(binRdr.ReadBytesRequired(sizeof(Int64)).Reverse(), 0);
        }

        /// <summary>
        /// Read a byte array from the current stream of a given count.
        /// </summary>
        /// <param name="binRdr">Binary reader.</param>
        /// <param name="byteCount">Bytes to read.</param>
        /// <returns>A byte array.</returns>
        public static byte[] ReadBytesRequired(this BinaryReader binRdr, int byteCount)
        {
            var result = binRdr.ReadBytes(byteCount);

            if (result.Length != byteCount)
                throw new EndOfStreamException($"{byteCount} bytes required from stream, but only {result.Length} returned.");

            return result;
        }

        /// <summary>
        /// Write a hexdump to the logger of the given bytes.
        /// </summary>
        /// <param name="bytes">Byte array.</param>
        /// <param name="offset">Start offset.</param>
        /// <param name="bytesPerLine">Count of bytes per line.</param>
        public static void Dump(this byte[] bytes, int offset = 0, int bytesPerLine = 16)
        {
            var hexChars = "0123456789ABCDEF".ToCharArray();

            var offsetBlock = 8 + 3;
            var byteBlock = offsetBlock + bytesPerLine * 3 + (bytesPerLine - 1) / 8 + 2;
            var lineLength = byteBlock + bytesPerLine + Environment.NewLine.Length;

            var line = (new string(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            var numLines = (bytes.Length + bytesPerLine - 1) / bytesPerLine;

            var sb = new StringBuilder(numLines * lineLength);
            sb.Append('\n');

            for (var i = 0; i < bytes.Length; i += bytesPerLine)
            {
                var h = i + offset;

                line[0] = hexChars[(h >> 28) & 0xF];
                line[1] = hexChars[(h >> 24) & 0xF];
                line[2] = hexChars[(h >> 20) & 0xF];
                line[3] = hexChars[(h >> 16) & 0xF];
                line[4] = hexChars[(h >> 12) & 0xF];
                line[5] = hexChars[(h >> 8) & 0xF];
                line[6] = hexChars[(h >> 4) & 0xF];
                line[7] = hexChars[(h >> 0) & 0xF];

                var hexColumn = offsetBlock;
                var charColumn = byteBlock;

                for (var j = 0; j < bytesPerLine; j++)
                {
                    if (j > 0 && (j & 7) == 0)
                    {
                        hexColumn++;
                    }

                    if (i + j >= bytes.Length)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        var by = bytes[i + j];
                        line[hexColumn] = hexChars[(by >> 4) & 0xF];
                        line[hexColumn + 1] = hexChars[by & 0xF];
                        line[charColumn] = by < 32 ? '.' : (char)by;
                    }

                    hexColumn += 3;
                    charColumn++;
                }

                sb.Append(line);
            }

            Log.Verbose(sb.ToString().TrimEnd(Environment.NewLine.ToCharArray()));
        }
    }
}
