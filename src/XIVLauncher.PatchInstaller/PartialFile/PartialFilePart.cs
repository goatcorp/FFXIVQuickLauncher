using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PartialFilePart : IComparable<PartialFilePart>
    {
        public static int SourceIndex_Zeros = -1;
        public static int SourceIndex_EmptyBlock = -2;
        public static int SourceIndex_Unavailable = -3;

        public long TargetOffset;
        public long SourceOffset;

        public int TargetSize;
        public int SourceIndex;
        public int SourceSize;
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

        public int CompareTo(PartialFilePart other)
        {
            var x = TargetOffset - other.TargetOffset;
            return x < 0 ? -1 : x > 0 ? 1 : 0;
        }

        public bool VerifyData(byte[] buf, int offset, int length)
        {
            if (Crc32Available)
            {
                Util.Crc32 crc32 = new();
                crc32.Init();
                crc32.Update(buf, 0, length);
                return crc32.Checksum == Crc32;
            }
            else if (IsAllZeros)
            {
                return buf.Skip(offset).Take(length).All(x => x == 0);
            }
            else if (IsEmptyBlock)
            {
                return BitConverter.ToInt32(buf, offset + 0) == 1 << 7
                    && BitConverter.ToInt32(buf, offset + 4) == 0
                    && BitConverter.ToInt32(buf, offset + 8) == 0
                    && BitConverter.ToInt32(buf, offset + 12) == (SourceSize >> 7) - 1
                    && buf.Skip(offset + 16).Take(length - 16).All(x => x == 0);
            }
            return false;
        }
    }
}
