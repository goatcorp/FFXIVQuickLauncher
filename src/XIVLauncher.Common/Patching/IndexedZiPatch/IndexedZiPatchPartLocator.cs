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
        public const byte SourceIndex_Zeros = byte.MaxValue - 0;
        public const byte SourceIndex_EmptyBlock = byte.MaxValue - 1;
        public const byte SourceIndex_Unavailable = byte.MaxValue - 2;
        public const byte SourceIndex_MaxValid = byte.MaxValue - 3;

        private const uint TargetSizeAndFlagMask_IsDeflatedBlockData = 0x80000000;
        private const uint TargetSizeAndFlagMask_IsValidCrc32Value = 0x40000000;
        private const uint TargetSizeAndFlagMask_TargetSize = 0x3FFFFFFF;

        private uint TargetOffsetUint;  // up to 35 bits, using only 32 bits (28 bits for locator + lsh 7; odd values exist), but currently .dat# files are delimited at 1.9GB
        private uint SourceOffsetUint;  // up to 31 bits (patch files were delimited at 1.5GB-ish; odd values exist)
        private uint TargetSizeAndFlags;  // 2 flag bits + up to 31 size bits, using only 30 bits (same with above)
        public uint Crc32OrPlaceholderEntryDataUnits;  // fixed 32 bits
        private ushort SplitDecodedSourceFromUshort;  // up to 14 bits (max value 15999)
        private byte TargetIndexByte;  // using only 8 bits for now
        private byte SourceIndexByte;  // using only 8 bits for now

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
            get => TargetSizeAndFlags & TargetSizeAndFlagMask_TargetSize;
            set => TargetSizeAndFlags = CheckedCastToUint((TargetSizeAndFlags & ~TargetSizeAndFlagMask_TargetSize) | value, TargetSizeAndFlagMask_TargetSize);
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
            get => 0 != (TargetSizeAndFlags & TargetSizeAndFlagMask_IsDeflatedBlockData);
            set => TargetSizeAndFlags = (TargetSizeAndFlags & ~TargetSizeAndFlagMask_IsDeflatedBlockData) | (value ? TargetSizeAndFlagMask_IsDeflatedBlockData : 0u);
        }

        public bool IsValidCrc32Value
        {
            get => 0 != (TargetSizeAndFlags & TargetSizeAndFlagMask_IsValidCrc32Value);
            set => TargetSizeAndFlags = (TargetSizeAndFlags & ~TargetSizeAndFlagMask_IsValidCrc32Value) | (value ? TargetSizeAndFlagMask_IsValidCrc32Value : 0u);
        }

        public bool IsAllZeros => SourceIndex == SourceIndex_Zeros;
        public bool IsEmptyBlock => SourceIndex == SourceIndex_EmptyBlock;
        public bool IsUnavailable => SourceIndex == SourceIndex_Unavailable;
        public bool IsFromSourceFile => !IsAllZeros && !IsEmptyBlock && !IsUnavailable;

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory([Out] byte[] dest, ref IndexedZiPatchPartLocator src, int cb);
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(out IndexedZiPatchPartLocator dest, [In] byte[] src, int cb);

        public void WriteTo(BinaryWriter writer)
        {
            using var buf = ReusableByteBufferManager.GetBuffer();
            int unitSize = Marshal.SizeOf<IndexedZiPatchPartLocator>();
            CopyMemory(buf.Buffer, ref this, unitSize);
            writer.Write(buf.Buffer, 0, unitSize);
        }

        public void ReadFrom(BinaryReader reader)
        {
            using var buf = ReusableByteBufferManager.GetBuffer();
            int unitSize = Marshal.SizeOf<IndexedZiPatchPartLocator>();
            reader.Read(buf.Buffer, 0, unitSize);
            CopyMemory(out this, buf.Buffer, unitSize);
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
            {
                Common.Patching.Util.Crc32 crc32 = new();
                crc32.Init();
                crc32.Update(buf, 0, length);
                return crc32.Checksum == Crc32OrPlaceholderEntryDataUnits ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;
            }
            else if (IsAllZeros)
            {
                return buf.Skip(offset).Take(length).All(x => x == 0) ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;
            }
            else if (IsEmptyBlock)
            {
                return BitConverter.ToInt32(buf, offset + 0) == 1 << 7
                    && BitConverter.ToInt32(buf, offset + 4) == 0
                    && BitConverter.ToInt32(buf, offset + 8) == 0
                    && BitConverter.ToInt32(buf, offset + 12) == Crc32OrPlaceholderEntryDataUnits
                    && buf.Skip(offset + 16).Take(length - 16).All(x => x == 0) ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;
            }
            return VerifyDataResult.FailUnverifiable;
        }

        public VerifyDataResult Verify(Stream stream, bool seek = true)
        {
            using var buffer = Common.Patching.Util.ReusableByteBufferManager.GetBuffer();
            if (seek)
                stream.Seek(TargetOffset, SeekOrigin.Begin);

            if (IsValidCrc32Value)
            {
                Common.Patching.Util.Crc32 crc32 = new();
                crc32.Init();
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
                for (long remaining = TargetSize, i = 0; remaining > 0; remaining -= buffer.Buffer.Length, i++)
                {
                    var readSize = (int)Math.Min(remaining, buffer.Buffer.Length);
                    if (readSize != stream.Read(buffer.Buffer, 0, readSize))
                        return VerifyDataResult.FailNotEnoughData;
                    if (i == 0)
                    {
                        // File entry header for placeholder
                        if (BitConverter.ToInt32(buffer.Buffer, 0) != 1 << 7
                            || BitConverter.ToInt32(buffer.Buffer, 4) != 0
                            || BitConverter.ToInt32(buffer.Buffer, 8) != 0
                            || BitConverter.ToInt32(buffer.Buffer, 12) != Crc32OrPlaceholderEntryDataUnits
                            || !buffer.Buffer.Skip(16).Take(readSize - 16).All(x => x == 0))
                            return VerifyDataResult.FailBadData;
                    }
                    else
                    {
                        // Remainder of the entry which should be all zeros
                        if (!buffer.Buffer.Take(readSize).All(x => x == 0))
                            return VerifyDataResult.FailBadData;
                    }
                }
                return VerifyDataResult.Pass;
            }

            return VerifyDataResult.FailUnverifiable;
        }

        public int Reconstruct(IList<Stream> sources, byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0)
        {
            if (IsFromSourceFile)
                return Reconstruct(sources[SourceIndex], buffer, bufferOffset, bufferSize, relativeOffset);
            return Reconstruct(null, 0, buffer, bufferOffset, bufferSize, relativeOffset);
        }

        public int Reconstruct(byte[] sourceSegment, int sourceSegmentOffset, byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0)
        {
            if (bufferSize == -1)
                bufferSize = (int)Math.Max(0, Math.Min(TargetSize - relativeOffset, buffer.Length));
            else if (bufferSize > TargetSize - relativeOffset)
                bufferSize = (int)Math.Max(0, TargetSize - relativeOffset);
            else if (bufferSize < 0)
                throw new ArgumentException("Length cannot be less than zero.");

            if (bufferSize == 0)
                return 0;

            if (IsUnavailable)
            {
                throw new InvalidOperationException("Unavailable part read attempt");
            }
            else if (IsAllZeros)
            {
                Array.Clear(buffer, bufferOffset, bufferSize);
            }
            else if (IsEmptyBlock)
            {
                Array.Clear(buffer, bufferOffset, bufferSize);

                if (relativeOffset < 16)
                {
                    using var buffer2 = Common.Patching.Util.ReusableByteBufferManager.GetBuffer();
                    buffer2.Writer.Write(1 << 7);
                    buffer2.Writer.Write(0);
                    buffer2.Writer.Write(0);
                    buffer2.Writer.Write((int)Crc32OrPlaceholderEntryDataUnits);
                    Array.Copy(buffer2.Buffer, relativeOffset, buffer, bufferOffset, Math.Min(bufferSize, 16 - relativeOffset));
                }
            }
            else if (IsDeflatedBlockData)
            {
                using var inflatedBuffer = Common.Patching.Util.ReusableByteBufferManager.GetBuffer(14);  // 16384
                using (var stream = new DeflateStream(new MemoryStream(sourceSegment, sourceSegmentOffset, sourceSegment.Length - sourceSegmentOffset), CompressionMode.Decompress, true))
                    stream.Read(inflatedBuffer.Buffer, 0, inflatedBuffer.Buffer.Length);
                if (VerifyDataResult.Pass != Verify(inflatedBuffer.Buffer, (int)SplitDecodedSourceFrom, (int)TargetSize))
                    throw new IOException("Verify failed on reconstruct");
                Array.Copy(inflatedBuffer.Buffer, SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }
            else
            {
                if (VerifyDataResult.Pass != Verify(sourceSegment, (int)(sourceSegmentOffset + SplitDecodedSourceFrom), (int)TargetSize))
                    throw new IOException("Verify failed on reconstruct");
                Array.Copy(sourceSegment, sourceSegmentOffset + SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }
            return bufferSize;
        }

        public class InsufficientReconstructionDataException : IOException
        {
            public InsufficientReconstructionDataException(string msg) : base(msg) { }
        }

        public int Reconstruct(Stream source, byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0)
        {
            if (!IsFromSourceFile)
                return Reconstruct(null, 0, buffer, bufferOffset, bufferSize, relativeOffset);

            if (bufferSize == -1)
                bufferSize = (int)Math.Max(0, Math.Min(TargetSize - relativeOffset, buffer.Length));
            else if (bufferSize > TargetSize - relativeOffset)
                bufferSize = (int)Math.Max(0, TargetSize - relativeOffset);
            else if (bufferSize < 0)
                throw new ArgumentException("Length cannot be less than zero.");

            if (IsDeflatedBlockData)
            {
                source.Seek(SourceOffset, SeekOrigin.Begin);
                using var inflatedBuffer = Common.Patching.Util.ReusableByteBufferManager.GetBuffer(14);  // 16384
                try
                {
                    using var stream = new DeflateStream(source, CompressionMode.Decompress, true);
                    var read = stream.Read(inflatedBuffer.Buffer, 0, inflatedBuffer.Buffer.Length);
                    if (read < SplitDecodedSourceFrom + bufferSize)
                        throw new InsufficientReconstructionDataException("Not enough inflated data read");
                }
                catch (InvalidDataException)
                {
                    throw new InsufficientReconstructionDataException("Not enough inflated data read, or corrupt zlib data");
                }
                Array.Copy(inflatedBuffer.Buffer, SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }
            else
            {
                source.Seek(SourceOffset + SplitDecodedSourceFrom + relativeOffset, SeekOrigin.Begin);
                if (bufferSize != source.Read(buffer, bufferOffset, bufferSize))
                    throw new InsufficientReconstructionDataException("Not enough source data read");
            }
            return bufferSize;
        }

        public static void CalculateCrc32(ref IndexedZiPatchPartLocator part, Stream source)
        {
            if (part.IsValidCrc32Value)
                return;

            using var buffer = Common.Patching.Util.ReusableByteBufferManager.GetBuffer(22);  // 4MB
            Common.Patching.Util.Crc32 crc32 = new();
            crc32.Init();
            for (int relativeOffset = 0; relativeOffset < part.TargetSize; relativeOffset += buffer.Buffer.Length)
            {
                var readSize = (int)Math.Min(part.TargetSize - relativeOffset, buffer.Buffer.Length);
                if (readSize != part.Reconstruct(source, buffer.Buffer, 0, readSize, relativeOffset))
                    throw new EndOfStreamException("Encountered premature end of file while trying to read the source stream.");
                crc32.Update(buffer.Buffer, 0, readSize);
            }
            part.Crc32OrPlaceholderEntryDataUnits = crc32.Checksum;
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
