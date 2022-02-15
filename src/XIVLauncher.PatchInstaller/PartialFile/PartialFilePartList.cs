using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public partial class PartialFilePartList : IList<PartialFilePart>
    {
        private readonly List<PartialFilePart> Underlying = new();

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

        public long FileSize => Underlying.Count > 0 ? Underlying.Last().TargetEnd : 0;

        public int BinarySearchByTargetOffset(long targetOffset)
        {
            return Underlying.BinarySearch(new PartialFilePart { TargetOffset = targetOffset }); ;
        }

        public void SplitAt(long offset, int targetFileIndex)
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
                    TargetSize = offset,
                    TargetIndex = targetFileIndex,
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
                    TargetSize = offset - Underlying[i - 1].TargetEnd,
                    TargetIndex = targetFileIndex,
                    SourceIndex = PartialFilePart.SourceIndex_Zeros,
                });
            }
            else
            {
                i -= 1;
                var part = Underlying[i];

                if (part.IsDeflatedBlockData || part.IsEmptyBlock)
                {
                    Underlying[i] = new PartialFilePart
                    {
                        TargetOffset = part.TargetOffset,
                        TargetSize = offset - part.TargetOffset,
                        TargetIndex = targetFileIndex,
                        SourceIndex = part.SourceIndex,
                        SourceOffset = part.SourceOffset,
                        SplitDecodedSourceFrom = part.SplitDecodedSourceFrom,
                        Crc32OrPlaceholderEntryDataUnits = part.Crc32OrPlaceholderEntryDataUnits,
                        IsDeflatedBlockData = part.IsDeflatedBlockData,
                    };
                    Underlying.Insert(i + 1, new PartialFilePart
                    {
                        TargetOffset = offset,
                        TargetSize = part.TargetEnd - offset,
                        TargetIndex = targetFileIndex,
                        SourceIndex = part.SourceIndex,
                        SourceOffset = part.SourceOffset,
                        SplitDecodedSourceFrom = part.SplitDecodedSourceFrom + offset - part.TargetOffset,
                        Crc32OrPlaceholderEntryDataUnits = part.Crc32OrPlaceholderEntryDataUnits,
                        IsDeflatedBlockData = part.IsDeflatedBlockData,
                    });
                }
                else
                {
                    if (part.SplitDecodedSourceFrom != 0)
                        throw new ArgumentException("Not deflated but SplitDecodeSourceFrom is given");

                    Underlying[i] = new PartialFilePart
                    {
                        TargetOffset = part.TargetOffset,
                        TargetSize = offset - part.TargetOffset,
                        TargetIndex = targetFileIndex,
                        SourceIndex = part.SourceIndex,
                        SourceOffset = part.SourceOffset,
                        Crc32OrPlaceholderEntryDataUnits = part.Crc32OrPlaceholderEntryDataUnits,
                    };
                    Underlying.Insert(i + 1, new PartialFilePart
                    {
                        TargetOffset = offset,
                        TargetSize = part.TargetEnd - offset,
                        TargetIndex = targetFileIndex,
                        SourceIndex = part.SourceIndex,
                        SourceOffset = part.SourceOffset + offset - part.TargetOffset,
                        Crc32OrPlaceholderEntryDataUnits = part.Crc32OrPlaceholderEntryDataUnits,
                    });
                }
            }
        }

        public void Update(PartialFilePart part)
        {
            if (part.TargetSize == 0)
                return;

            SplitAt(part.TargetOffset, part.TargetIndex);
            SplitAt(part.TargetEnd, part.TargetIndex);

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

        public void CalculateCrc32(List<Stream> sources)
        {
            var list = Underlying.ToArray();
            for (var i = 0; i < list.Length; ++i)
                if (list[i].IsFromSourceFile)
                PartialFilePart.CalculateCrc32(ref list[i], sources[list[i].SourceIndex]);
            Underlying.Clear();
            Underlying.AddRange(list);
        }

        public Stream ToStream(List<Stream> sources)
        {
            return new PartialFileViewStream(sources, this);
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
