using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    public partial class IndexedZiPatchTargetFile : IList<IndexedZiPatchPartLocator>
    {
        public string RelativePath = "";
        private readonly List<IndexedZiPatchPartLocator> Underlying = new();

        public IndexedZiPatchTargetFile() : base() { }

        public IndexedZiPatchTargetFile(string fileName) : base() {
            RelativePath = fileName;
        }

        public IndexedZiPatchTargetFile(BinaryReader reader, bool disposeReader = true) : base()
        {
            try
            {
                ReadFrom(reader);
            }
            finally
            {
                if (disposeReader)
                    reader.Dispose();
            }
        }

        public IndexedZiPatchPartLocator this[int index] { get => Underlying[index]; set => Underlying[index] = value; }

        public int Count => Underlying.Count;

        public bool IsReadOnly => false;

        public void Add(IndexedZiPatchPartLocator item) => Underlying.Add(item);

        public void Clear() => Underlying.Clear();

        public bool Contains(IndexedZiPatchPartLocator item) => Underlying.Contains(item);

        public void CopyTo(IndexedZiPatchPartLocator[] array, int arrayIndex) => Underlying.CopyTo(array, arrayIndex);

        public IEnumerator<IndexedZiPatchPartLocator> GetEnumerator() => Underlying.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Underlying.GetEnumerator();

        public int IndexOf(IndexedZiPatchPartLocator item) => Underlying.IndexOf(item);

        public void Insert(int index, IndexedZiPatchPartLocator item) => Underlying.Insert(index, item);

        public bool Remove(IndexedZiPatchPartLocator item) => Underlying.Remove(item);

        public void RemoveAt(int index) => Underlying.RemoveAt(index);

        public long FileSize => Underlying.Count > 0 ? Underlying.Last().TargetEnd : 0;

        public int BinarySearchByTargetOffset(long targetOffset)
        {
            return Underlying.BinarySearch(new IndexedZiPatchPartLocator { TargetOffset = targetOffset }); ;
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
                Underlying.Add(new IndexedZiPatchPartLocator
                {
                    TargetSize = offset,
                    TargetIndex = targetFileIndex,
                    SourceIndex = IndexedZiPatchPartLocator.SourceIndex_Zeros,
                });
            }
            else if (i == Underlying.Count && Underlying[i - 1].TargetEnd == offset)
            {
                // Do nothing; split at TargetEnd of last part is give
            }
            else if (i == Underlying.Count && Underlying[i - 1].TargetEnd < offset)
            {
                Underlying.Add(new IndexedZiPatchPartLocator
                {
                    TargetOffset = Underlying[i - 1].TargetEnd,
                    TargetSize = offset - Underlying[i - 1].TargetEnd,
                    TargetIndex = targetFileIndex,
                    SourceIndex = IndexedZiPatchPartLocator.SourceIndex_Zeros,
                });
            }
            else
            {
                i -= 1;
                var part = Underlying[i];

                if (part.IsDeflatedBlockData || part.IsEmptyBlock)
                {
                    Underlying[i] = new IndexedZiPatchPartLocator
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
                    Underlying.Insert(i + 1, new IndexedZiPatchPartLocator
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

                    Underlying[i] = new IndexedZiPatchPartLocator
                    {
                        TargetOffset = part.TargetOffset,
                        TargetSize = offset - part.TargetOffset,
                        TargetIndex = targetFileIndex,
                        SourceIndex = part.SourceIndex,
                        SourceOffset = part.SourceOffset,
                        Crc32OrPlaceholderEntryDataUnits = part.Crc32OrPlaceholderEntryDataUnits,
                    };
                    Underlying.Insert(i + 1, new IndexedZiPatchPartLocator
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

        public void Update(IndexedZiPatchPartLocator part)
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

        public async Task CalculateCrc32(List<Stream> sources, CancellationToken? cancellationToken = null)
        {
            await Task.Run(() =>
            {
                var list = Underlying.ToArray();
                for (var i = 0; i < list.Length; ++i)
                {
                    if (cancellationToken.HasValue)
                        cancellationToken.Value.ThrowIfCancellationRequested();
                    if (list[i].IsFromSourceFile)
                        IndexedZiPatchPartLocator.CalculateCrc32(ref list[i], sources[list[i].SourceIndex]);
                }
                Underlying.Clear();
                Underlying.AddRange(list);
            });
        }

        public Stream ToStream(List<Stream> sources)
        {
            return new IndexedZiPatchTargetViewStream(sources, this);
        }

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory([Out] byte[] dest, ref IndexedZiPatchPartLocator src, int cb);
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(out IndexedZiPatchPartLocator dest, [In] byte[] src, int cb);

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(RelativePath);
            writer.Write(Underlying.Count);
            foreach (var item in Underlying) 
                item.WriteTo(writer);
        }

        public void ReadFrom(BinaryReader reader)
        {
            RelativePath = reader.ReadString();
            var dest = new IndexedZiPatchPartLocator[reader.ReadInt32()];
            for (var i = 0; i < dest.Length; ++i)
                dest[i].ReadFrom(reader);
            Underlying.Clear();
            Underlying.AddRange(dest);
        }
    }
}
