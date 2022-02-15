using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PartialFilePart : IComparable<PartialFilePart>
    {
        public static short SourceIndex_Zeros = -1;
        public static short SourceIndex_EmptyBlock = -2;
        public static short SourceIndex_Unavailable = -3;

        public long TargetOffset;
        public long SourceOffset;

        public int TargetSize;
        public int SourceSize;

        public short TargetIndex;
        public short SourceIndex;

        public int SplitDecodedSourceFrom;

        public uint Crc32;
        public int Flags;

        public long TargetEnd => TargetOffset + TargetSize;

        public bool SourceIsDeflated
        {
            get => 0 != (Flags & 1);
            set => Flags = (Flags & ~1) | (value ? 1 : 0);
        }

        public bool Crc32Available
        {
            get => 0 != (Flags & 2);
            set => Flags = (Flags & ~2) | (value ? 2 : 0);
        }

        public bool IsAllZeros => SourceIndex == SourceIndex_Zeros;
        public bool IsEmptyBlock => SourceIndex == SourceIndex_EmptyBlock;
        public bool IsUnavailable => SourceIndex == SourceIndex_Unavailable;
        public bool IsFromSourceFile => !IsAllZeros && !IsEmptyBlock && !IsUnavailable;

        public int CompareTo(PartialFilePart other)
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

            if (Crc32Available)
            {
                Util.Crc32 crc32 = new();
                crc32.Init();
                crc32.Update(buf, 0, length);
                return crc32.Checksum == Crc32 ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;
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
                    && BitConverter.ToInt32(buf, offset + 12) == (SourceSize >> 7) - 1
                    && buf.Skip(offset + 16).Take(length - 16).All(x => x == 0) ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;
            }
            return VerifyDataResult.FailUnverifiable;
        }

        public VerifyDataResult Verify(Stream stream, bool seek = true)
        {
            using var buffer = Util.ReusableByteBufferManager.GetBuffer();
            if (seek)
                stream.Seek(TargetOffset, SeekOrigin.Begin);

            if (Crc32Available)
            {
                Util.Crc32 crc32 = new();
                crc32.Init();
                for (var remaining = TargetSize; remaining > 0; remaining -= buffer.Buffer.Length)
                {
                    var readSize = Math.Min(remaining, buffer.Buffer.Length);
                    if (readSize != stream.Read(buffer.Buffer, 0, readSize))
                        return VerifyDataResult.FailNotEnoughData;
                    crc32.Update(buffer.Buffer, 0, readSize);
                }
                return crc32.Checksum == Crc32 ? VerifyDataResult.Pass : VerifyDataResult.FailBadData;
            }
            else if (IsAllZeros)
            {
                for (var remaining = TargetSize; remaining > 0; remaining -= buffer.Buffer.Length)
                {
                    var readSize = Math.Min(remaining, buffer.Buffer.Length);
                    if (readSize != stream.Read(buffer.Buffer, 0, readSize))
                        return VerifyDataResult.FailNotEnoughData;
                    if (!buffer.Buffer.Take(readSize).All(x => x == 0))
                        return VerifyDataResult.FailBadData;
                }
                return VerifyDataResult.Pass;
            }
            else if (IsEmptyBlock)
            {
                for (int remaining = TargetSize - buffer.Buffer.Length, i = 0; remaining > 0; remaining -= buffer.Buffer.Length, i++)
                {
                    var readSize = Math.Min(remaining, buffer.Buffer.Length);
                    if (readSize != stream.Read(buffer.Buffer, 0, readSize))
                        return VerifyDataResult.FailNotEnoughData;
                    if (i == 0)
                    {
                        // File entry header for placeholder
                        if (BitConverter.ToInt32(buffer.Buffer, 0) != 1 << 7
                            || BitConverter.ToInt32(buffer.Buffer, 4) != 0
                            || BitConverter.ToInt32(buffer.Buffer, 8) != 0
                            || BitConverter.ToInt32(buffer.Buffer, 12) != (SourceSize >> 7) - 1
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
                bufferSize = Math.Max(0, Math.Min(TargetSize - relativeOffset, buffer.Length));
            else if (bufferSize > TargetSize - relativeOffset)
                bufferSize = Math.Max(0, TargetSize - relativeOffset);
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
                    using var buffer2 = Util.ReusableByteBufferManager.GetBuffer();
                    buffer2.Writer.Write(1 << 7);
                    buffer2.Writer.Write(0);
                    buffer2.Writer.Write(0);
                    buffer2.Writer.Write((SourceSize >> 7) - 1);
                    Array.Copy(buffer2.Buffer, relativeOffset, buffer, bufferOffset, Math.Min(bufferSize, 16 - relativeOffset));
                }
            }
            else if (SourceIsDeflated)
            {
                using var inflatedBuffer = Util.ReusableByteBufferManager.GetBuffer(14);  // 16384
                using (var stream = new DeflateStream(new MemoryStream(sourceSegment, sourceSegmentOffset, sourceSegment.Length - sourceSegmentOffset), CompressionMode.Decompress, true))
                    stream.Read(inflatedBuffer.Buffer, 0, inflatedBuffer.Buffer.Length);
                Array.Copy(inflatedBuffer.Buffer, SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }
            else
            {
                Array.Copy(sourceSegment, sourceSegmentOffset + SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }
            return bufferSize;
        }

        public int Reconstruct(Stream source, byte[] buffer, int bufferOffset = 0, int bufferSize = -1, int relativeOffset = 0)
        {
            if (!IsFromSourceFile)
                return Reconstruct(null, 0, buffer, bufferOffset, bufferSize, relativeOffset);

            if (bufferSize == -1)
                bufferSize = Math.Max(0, Math.Min(TargetSize - relativeOffset, buffer.Length));
            else if (bufferSize > TargetSize - relativeOffset)
                bufferSize = Math.Max(0, TargetSize - relativeOffset);
            else if (bufferSize < 0)
                throw new ArgumentException("Length cannot be less than zero.");

            if (SourceIsDeflated)
            {
                using var deflatedBuffer = Util.ReusableByteBufferManager.GetBuffer(14);  // 16384
                source.Seek(SourceOffset, SeekOrigin.Begin);
                if (SourceSize != source.Read(deflatedBuffer.Buffer, 0, SourceSize))
                    throw new IOException("Failed to read full part of source file");

                using var inflatedBuffer = Util.ReusableByteBufferManager.GetBuffer(14);  // 16384
                using (var stream = new DeflateStream(deflatedBuffer.Stream, CompressionMode.Decompress, true))
                    stream.Read(inflatedBuffer.Buffer, 0, inflatedBuffer.Buffer.Length);
                Array.Copy(inflatedBuffer.Buffer, SplitDecodedSourceFrom + relativeOffset, buffer, bufferOffset, bufferSize);
            }
            else
            {
                source.Seek(SourceOffset + SplitDecodedSourceFrom + relativeOffset, SeekOrigin.Begin);
                if (bufferSize != source.Read(buffer, bufferOffset, bufferSize))
                    throw new IOException("Failed to read full part of source file");
            }
            return bufferSize;
        }

        public void Repair(Stream target, Stream source)
        {
            using var buffer = Util.ReusableByteBufferManager.GetBuffer(22);  // 4MB
            if (target.Length < TargetOffset)
                target.SetLength(target.Length);
            target.Seek(TargetOffset, SeekOrigin.Begin);
            for (int relativeOffset = 0; relativeOffset < TargetSize; relativeOffset += buffer.Buffer.Length)
            {
                var readSize = Math.Min(TargetSize - relativeOffset, buffer.Buffer.Length);
                if (readSize != Reconstruct(source, buffer.Buffer, 0, readSize, relativeOffset))
                    throw new EndOfStreamException("Encountered premature end of file while trying to read the source stream.");
                target.Write(buffer.Buffer, 0, readSize);
            }
        }

        public void Repair(Stream target, IList<Stream> sources)
        {
            Repair(target, IsFromSourceFile ? sources[SourceIndex] : null);
        }

        public static void CalculateCrc32(ref PartialFilePart part, Stream source)
        {
            if (part.Crc32Available)
                return;

            using var buffer = Util.ReusableByteBufferManager.GetBuffer(22);  // 4MB
            Util.Crc32 crc32 = new();
            crc32.Init();
            for (int relativeOffset = 0; relativeOffset < part.TargetSize; relativeOffset += buffer.Buffer.Length)
            {
                var readSize = Math.Min(part.TargetSize - relativeOffset, buffer.Buffer.Length);
                if (readSize != part.Reconstruct(source, buffer.Buffer, 0, readSize, relativeOffset))
                    throw new EndOfStreamException("Encountered premature end of file while trying to read the source stream.");
                crc32.Update(buffer.Buffer, 0, readSize);
            }
            part.Crc32 = crc32.Checksum;
            part.Crc32Available = true;
        }
    }
}
