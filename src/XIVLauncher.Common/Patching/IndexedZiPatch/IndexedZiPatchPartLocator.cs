using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct IndexedZiPatchPartLocator : IComparable<IndexedZiPatchPartLocator>
    {
        public const byte SOURCE_INDEX_ZEROS = byte.MaxValue - 0;
        public const byte SOURCE_INDEX_EMPTY_BLOCK = byte.MaxValue - 1;
        public const byte SOURCE_INDEX_UNAVAILABLE = byte.MaxValue - 2;
        public const byte SOURCE_INDEX_MAX_VALID = byte.MaxValue - 3;

        private const uint TARGET_SIZE_AND_FLAG_MASK_IS_DEFLATED_BLOCK_DATA = 0x80000000;
        private const uint TARGET_SIZE_AND_FLAG_MASK_IS_VALID_CRC32_VALUE = 0x40000000;
        private const uint TARGET_SIZE_AND_FLAG_MASK_TARGET_SIZE = 0x3FFFFFFF;

        private uint TargetOffsetUint; // up to 35 bits, using only 32 bits (28 bits for locator + lsh 7; odd values exist), but currently .dat# files are delimited at 1.9GB
        private uint SourceOffsetUint; // up to 31 bits (patch files were delimited at 1.5GB-ish; odd values exist)
        private uint TargetSizeAndFlags; // 2 flag bits + up to 31 size bits, using only 30 bits (same with above)
        public uint Crc32OrPlaceholderEntryDataUnits; // fixed 32 bits
        private ushort SplitDecodedSourceFromUshort; // up to 14 bits (max value 15999)
        private byte TargetIndexByte; // using only 8 bits for now
        private byte SourceIndexByte; // using only 8 bits for now

        public long TargetOffset
        {
            get => TargetOffsetUint;
            set => TargetOffsetUint = CheckedCastToUint(value);
        }

        public long SourceOffset
        {
            get => SourceOffsetUint;
            set => SourceOffsetUint = CheckedCastToUint(value);
        }

        public long TargetSize
        {
            get => TargetSizeAndFlags & TARGET_SIZE_AND_FLAG_MASK_TARGET_SIZE;
            set => TargetSizeAndFlags = CheckedCastToUint((TargetSizeAndFlags & ~TARGET_SIZE_AND_FLAG_MASK_TARGET_SIZE) | value, TARGET_SIZE_AND_FLAG_MASK_TARGET_SIZE);
        }

        public long SplitDecodedSourceFrom
        {
            get => SplitDecodedSourceFromUshort;
            set => SplitDecodedSourceFromUshort = CheckedCastToUshort(value);
        }

        public int TargetIndex
        {
            get => TargetIndexByte;
            set => TargetIndexByte = CheckedCastToByte(value);
        }

        public int SourceIndex
        {
            get => SourceIndexByte;
            set => SourceIndexByte = CheckedCastToByte(value);
        }

        public long MaxSourceSize => IsDeflatedBlockData ? 16384 : TargetSize;
        public long MaxSourceEnd => SourceOffset + MaxSourceSize;
        public long TargetEnd => TargetOffset + TargetSize;

        public bool IsDeflatedBlockData
        {
            get => 0 != (TargetSizeAndFlags & TARGET_SIZE_AND_FLAG_MASK_IS_DEFLATED_BLOCK_DATA);
            set => TargetSizeAndFlags = (TargetSizeAndFlags & ~TARGET_SIZE_AND_FLAG_MASK_IS_DEFLATED_BLOCK_DATA) | (value ? TARGET_SIZE_AND_FLAG_MASK_IS_DEFLATED_BLOCK_DATA : 0u);
        }

        public bool IsValidCrc32Value
        {
            get => 0 != (TargetSizeAndFlags & TARGET_SIZE_AND_FLAG_MASK_IS_VALID_CRC32_VALUE);
            set => TargetSizeAndFlags = (TargetSizeAndFlags & ~TARGET_SIZE_AND_FLAG_MASK_IS_VALID_CRC32_VALUE) | (value ? TARGET_SIZE_AND_FLAG_MASK_IS_VALID_CRC32_VALUE : 0u);
        }

        public bool IsAllZeros => SourceIndex == SOURCE_INDEX_ZEROS;
        public bool IsEmptyBlock => SourceIndex == SOURCE_INDEX_EMPTY_BLOCK;
        public bool IsUnavailable => SourceIndex == SOURCE_INDEX_UNAVAILABLE;
        public bool IsFromSourceFile => !IsAllZeros && !IsEmptyBlock && !IsUnavailable;

        public unsafe void WriteTo(BinaryWriter writer)
        {
            int unitSize = Marshal.SizeOf(this);
            using var buf = ReusableByteBufferManager.GetBuffer(unitSize);

            fixed (byte* pBuf = buf.Buffer)
            {
                fixed (IndexedZiPatchPartLocator* pLocator = &this)
                {
                    Buffer.MemoryCopy(pLocator, pBuf, unitSize, unitSize);
                }
            }

            writer.Write(buf.Buffer, 0, unitSize);
        }

        public unsafe void ReadFrom(BinaryReader reader)
        {
            int unitSize = Marshal.SizeOf(this);
            using var buf = ReusableByteBufferManager.GetBuffer(unitSize);
            reader.Read(buf.Buffer, 0, unitSize);

            fixed (byte* pBuf = buf.Buffer)
            {
                fixed (IndexedZiPatchPartLocator* pLocator = &this)
                {
                    Buffer.MemoryCopy(pBuf, pLocator, unitSize, unitSize);
                }
            }
        }

        public int CompareTo(IndexedZiPatchPartLocator other)
        {
            var x = TargetOffset - other.TargetOffset;
            return x < 0 ? -1 : x > 0 ? 1 : 0;
        }

        public enum VerifyDataResult
        {
            Pass,
            FailUnverifiable,
            FailNotEnoughData,
            FailBadData,
        }

        public VerifyDataResult Verify(byte[] buf, int offset, int length)
        {
            if (length != TargetSize)
                return VerifyDataResult.FailNotEnoughData;

            if (IsValidCrc32Value)
                return Crc32.Calculate(buf, offset, length) == Crc32OrPlaceholderEntryDataUnits ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;

            if (IsAllZeros)
                return buf.Skip(offset).Take(length).All(x => x == 0) ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;

            if (IsEmptyBlock)
            {
                return BitConverter.ToInt32(buf, offset + 0) == 1 << 7
                       && BitConverter.ToInt32(buf, offset + 4) == 0
                       && BitConverter.ToInt32(buf, offset + 8) == 0
                       && BitConverter.ToInt32(buf, offset + 12) == this.Crc32OrPlaceholderEntryDataUnits
                       && BitConverter.ToInt32(buf, offset + 16) == 0
                       && BitConverter.ToInt32(buf, offset + 20) == 0
                       && buf.Skip(offset + 24).Take(length - 24).All(x => x == 0)
                    ? VerifyDataResult.Pass
                    : VerifyDataResult.FailBadData;
            }

            return VerifyDataResult.FailUnverifiable;
        }

        public VerifyDataResult Verify(Stream stream, bool seek = true)
        {
            using var buffer = ReusableByteBufferManager.GetBuffer();
            if (seek)
                stream.Seek(TargetOffset, SeekOrigin.Begin);

            if (IsValidCrc32Value)
            {
                Crc32 crc32 = new();

                for (var remaining = TargetSize; remaining > 0; remaining -= buffer.Buffer.Length)
                {
                    var readSize = (int)Math.Min(remaining, buffer.Buffer.Length);
                    if (readSize != stream.Read(buffer.Buffer, 0, readSize))
                        return VerifyDataResult.FailNotEnoughData;

                    crc32.Update(buffer.Buffer, 0, readSize);
                }

                if (crc32.Checksum != Crc32OrPlaceholderEntryDataUnits)
                    return VerifyDataResult.FailBadData;

                return VerifyDataResult.Pass;
            }
            else if (IsAllZeros)
            {
                for (var remaining = TargetSize; remaining > 0; remaining -= buffer.Buffer.Length)
                {
                    var readSize = (int)Math.Min(remaining, buffer.Buffer.Length);
                    if (readSize != stream.Read(buffer.Buffer, 0, readSize))
                        return VerifyDataResult.FailNotEnoughData;
                    if (!buffer.Buffer.Take(readSize).All(x => x == 0))
                        return VerifyDataResult.FailBadData;
                }

                return VerifyDataResult.Pass;
            }
            else if (IsEmptyBlock)
            {
                var readSize = Math.Min(1 << 7, buffer.Buffer.Length);
                if (readSize != stream.Read(buffer.Buffer, 0, readSize))
                    return VerifyDataResult.FailNotEnoughData;

                // File entry header for placeholder
                if (BitConverter.ToInt32(buffer.Buffer, 0) != 1 << 7
                    || BitConverter.ToInt32(buffer.Buffer, 4) != 0
                    || BitConverter.ToInt32(buffer.Buffer, 8) != 0
                    || BitConverter.ToInt32(buffer.Buffer, 12) != Crc32OrPlaceholderEntryDataUnits
                    || BitConverter.ToInt32(buffer.Buffer, 16) != 0
                    || BitConverter.ToInt32(buffer.Buffer, 20) != 0
                    || !buffer.Buffer.Skip(24).Take(readSize - 24).All(x => x == 0))
                    return VerifyDataResult.FailBadData;

                return VerifyDataResult.Pass;
            }

            return VerifyDataResult.FailUnverifiable;
        }

        public int Reconstruct(IList<Stream> sources, byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0, bool verify = true)
        {
            if (IsFromSourceFile)
                return Reconstruct(sources[SourceIndex], buffer, bufferOffset, bufferSize, relativeOffset, verify);

            return Reconstruct(null, 0, 0, buffer, bufferOffset, bufferSize, relativeOffset, verify);
        }

        private int FilterBufferSize(byte[] buffer, int bufferOffset, int bufferSize, int relativeOffset)
        {
            if (bufferSize == -1)
                return (int)Math.Max(0, Math.Min(TargetSize - relativeOffset, buffer.Length - bufferOffset));
            else if (bufferSize > TargetSize - relativeOffset)
                return (int)Math.Max(0, TargetSize - relativeOffset);
            else if (bufferSize < 0)
                throw new ArgumentException("Length cannot be less than zero.");
            else
                return bufferSize;
        }

        public int ReconstructWithoutSourceData(byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0)
        {
            bufferSize = FilterBufferSize(buffer, bufferOffset, bufferSize, relativeOffset);
            if (bufferSize == 0)
                return 0;

            if (IsUnavailable)
                throw new InvalidOperationException("Unavailable part read attempt");
            else if (IsAllZeros)
                Array.Clear(buffer, bufferOffset, bufferSize);
            else if (IsEmptyBlock)
            {
                Array.Clear(buffer, bufferOffset, bufferSize);

                if (relativeOffset < 16)
                {
                    using var buffer2 = ReusableByteBufferManager.GetBuffer();
                    buffer2.Writer.Write(1 << 7);
                    buffer2.Writer.Write(0);
                    buffer2.Writer.Write(0);
                    buffer2.Writer.Write((int)Crc32OrPlaceholderEntryDataUnits);
                    buffer2.Writer.Write(0);
                    buffer2.Writer.Write(0);
                    Array.Copy(buffer2.Buffer, relativeOffset, buffer, bufferOffset, Math.Min(bufferSize, 24 - relativeOffset));
                }
            }
            else
                throw new InvalidOperationException("This part requires source data.");

            return bufferSize;
        }

        public int Reconstruct(byte[] sourceSegment, int sourceSegmentOffset, int sourceSegmentLength, byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0, bool verify = true)
        {
            if (!IsFromSourceFile)
                return ReconstructWithoutSourceData(buffer, bufferOffset, bufferSize, relativeOffset);

            bufferSize = FilterBufferSize(buffer, bufferOffset, bufferSize, relativeOffset);
            if (bufferSize == 0)
                return 0;

            if (IsDeflatedBlockData)
            {
                using var inflatedBuffer = ReusableByteBufferManager.GetBuffer(MaxSourceSize);
                using (var stream = new DeflateStream(new MemoryStream(sourceSegment, sourceSegmentOffset, sourceSegmentLength - sourceSegmentOffset), CompressionMode.Decompress, true))
                    stream.Read(inflatedBuffer.Buffer, 0, inflatedBuffer.Buffer.Length);
                if (verify && VerifyDataResult.Pass != Verify(inflatedBuffer.Buffer, (int)SplitDecodedSourceFrom, (int)TargetSize))
                    throw new IOException("Verify failed on reconstruct (inflate)");

                Array.Copy(inflatedBuffer.Buffer, SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }
            else
            {
                if (sourceSegmentLength - sourceSegmentOffset < TargetSize)
                    throw new IOException("Insufficient source data");
                if (verify && VerifyDataResult.Pass != Verify(sourceSegment, (int)(sourceSegmentOffset + SplitDecodedSourceFrom), (int)TargetSize))
                    throw new IOException("Verify failed on reconstruct");

                Array.Copy(sourceSegment, sourceSegmentOffset + SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }

            return bufferSize;
        }

        public int Reconstruct(Stream source, byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0, bool verify = true)
        {
            if (!IsFromSourceFile)
                return ReconstructWithoutSourceData(buffer, bufferOffset, bufferSize, relativeOffset);

            bufferSize = FilterBufferSize(buffer, bufferOffset, bufferSize, relativeOffset);
            if (bufferSize == 0)
                return 0;

            source.Seek(SourceOffset, SeekOrigin.Begin);
            var readSize = (int)(IsDeflatedBlockData ? 16384 : TargetSize);
            using var readBuffer = ReusableByteBufferManager.GetBuffer(readSize);
            var read = source.Read(readBuffer.Buffer, 0, readSize);
            return Reconstruct(readBuffer.Buffer, 0, read, buffer, bufferOffset, bufferSize, relativeOffset, verify);
        }

        public static void CalculateCrc32(ref IndexedZiPatchPartLocator part, Stream source)
        {
            if (part.IsValidCrc32Value)
                return;

            using var buffer = ReusableByteBufferManager.GetBuffer(part.TargetSize);
            if (part.TargetSize != part.Reconstruct(source, buffer.Buffer, 0, (int)part.TargetSize, 0, false))
                throw new EndOfStreamException("Encountered premature end of file while trying to read the source stream.");

            part.Crc32OrPlaceholderEntryDataUnits = Crc32.Calculate(buffer.Buffer, 0, (int)part.TargetSize);
            part.IsValidCrc32Value = true;
        }

        private static uint CheckedCastToUint(long v, long maxValue = uint.MaxValue)
        {
            if (v > maxValue)
                throw new ArgumentException("Value too big");

            return (uint)v;
        }

        private static ushort CheckedCastToUshort(long v, long maxValue = ushort.MaxValue)
        {
            if (v > maxValue)
                throw new ArgumentException("Value too big");

            return (ushort)v;
        }

        private static byte CheckedCastToByte(long v, long maxValue = byte.MaxValue)
        {
            if (v > maxValue)
                throw new ArgumentException("Value too big");

            return (byte)v;
        }
    }
}