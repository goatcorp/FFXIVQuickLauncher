using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public class PartialFilePartList : IList<PartialFilePart>
    {
        internal readonly List<PartialFilePart> Underlying = new();

        public PartialFilePart this[int index] { get => Underlying[index]; set => Underlying[index] = value; }

        public int Count => Underlying.Count;

        public bool IsReadOnly => false;

        public void Add(PartialFilePart item) => Underlying.Add(item);

        public void Clear() => Underlying.Clear();

        public bool Contains(PartialFilePart item) => Underlying.Contains(item);

        public void CopyTo(PartialFilePart[] array, int arrayIndex) => Underlying.CopyTo(array, arrayIndex);

        public IEnumerator<PartialFilePart> GetEnumerator() => Underlying.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Underlying.GetEnumerator();

        public int IndexOf(PartialFilePart item) => Underlying.IndexOf(item);

        public void Insert(int index, PartialFilePart item) => Underlying.Insert(index, item);

        public bool Remove(PartialFilePart item) => Underlying.Remove(item);

        public void RemoveAt(int index) => Underlying.RemoveAt(index);

        public int BinarySearchByTargetOffset(long targetOffset)
        {
            return Underlying.BinarySearch(new PartialFilePart { TargetOffset = targetOffset }); ;
        }

        public void SplitAt(long offset)
        {
            var i = BinarySearchByTargetOffset(offset);
            if (i >= 0)
            {
                // Already split at given offset
                return;
            }

            i = ~i;
            if (i == 0 && offset == 0)
            {
                // Do nothing; split at 0 is a given
            }
            else if (i == 0 && Underlying.Count == 0)
            {
                Underlying.Add(new PartialFilePart
                {
                    TargetSize = (int)offset,
                    SourceIndex = PartialFilePart.SourceIndex_Zeros,
                });
            }
            else if (i == Underlying.Count && Underlying[i - 1].TargetEnd == offset)
            {
                // Do nothing; split at TargetEnd of last part is give
            }
            else if (i == Underlying.Count && Underlying[i - 1].TargetEnd < offset)
            {
                Underlying.Add(new PartialFilePart
                {
                    TargetOffset = Underlying[i - 1].TargetEnd,
                    TargetSize = (int)(offset - Underlying[i - 1].TargetEnd),
                    SourceIndex = PartialFilePart.SourceIndex_Zeros,
                });
            }
            else
            {
                i -= 1;
                var part = Underlying[i];
                Underlying[i] = new PartialFilePart
                {
                    TargetOffset = part.TargetOffset,
                    TargetSize = (int)(offset - part.TargetOffset),
                    SourceIndex = part.SourceIndex,
                    SourceOffset = part.SourceOffset,
                    SourceSize = part.SourceSize,
                    SplitDecodedSourceFrom = part.SplitDecodedSourceFrom,
                    SourceIsDeflated = part.SourceIsDeflated,
                };
                Underlying.Insert(i + 1, new PartialFilePart
                {
                    TargetOffset = offset,
                    TargetSize = (int)(part.TargetEnd - offset),
                    SourceIndex = part.SourceIndex,
                    SourceOffset = part.SourceOffset,
                    SourceSize = part.SourceSize,
                    SplitDecodedSourceFrom = (int)(part.SplitDecodedSourceFrom + offset - part.TargetOffset),
                    SourceIsDeflated = part.SourceIsDeflated,
                });
            }
        }

        public void Update(PartialFilePart part)
        {
            if (part.TargetSize == 0)
                return;

            SplitAt(part.TargetOffset);
            SplitAt(part.TargetEnd);

            var left = BinarySearchByTargetOffset(part.TargetOffset);
            if (left < 0)
                left = ~left;

            if (left == Underlying.Count)
            {
                Underlying.Add(part);
                return;
            }

            var right = BinarySearchByTargetOffset(part.TargetEnd);
            if (right < 0)
                right = ~right;

            if (right - left - 1 < 0)
                Debugger.Break();

            Underlying[left] = part;
            Underlying.RemoveRange(left + 1, right - left - 1);
        }

        public void CalculateCrc32(PartialFileViewStream stream)
        {
            Util.Crc32 crc32 = new();
            var list = Underlying.ToArray();
            byte[] buf = new byte[16000];
            for (var i = 0; i < list.Length; ++i)
            {
                if (list[i].Crc32Available || list[i].IsAllZeros || list[i].IsEmptyBlock)
                    continue;

                if (buf.Length < list[i].TargetSize)
                    buf = new byte[list[i].TargetSize];
                stream.Seek(list[i].TargetOffset, SeekOrigin.Begin);
                stream.Read(buf, 0, list[i].TargetSize);

                crc32.Init();
                crc32.Update(buf, 0, list[i].TargetSize);
                list[i].Crc32 = crc32.Checksum;
                list[i].Crc32Available = true;
            }
            Underlying.Clear();
            Underlying.AddRange(list);
        }

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory([Out] byte[] dest, [In] PartialFilePart[] src, int cb);
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory([Out] PartialFilePart[] dest, [In] byte[] src, int cb);

        public byte[] ToBytes()
        {
            var src = Underlying.ToArray();
            var dst = new byte[Marshal.SizeOf<PartialFilePart>() * src.Length];
            CopyMemory(dst, src, dst.Length);
            return dst;
        }

        public void FromBytes(byte[] src)
        {
            var dest = new PartialFilePart[src.Length / Marshal.SizeOf<PartialFilePart>()];
            CopyMemory(dest, src, src.Length);
            Underlying.Clear();
            Underlying.AddRange(dest);
        }
    }
}
