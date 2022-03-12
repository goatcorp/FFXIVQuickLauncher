using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    public partial class IndexedZiPatchTargetFile : IList<IndexedZiPatchPartLocator>
    {
        public string RelativePath = "";
        private readonly List<IndexedZiPatchPartLocator> underlying = new();

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

        public IndexedZiPatchPartLocator this[int index] { get => this.underlying[index]; set => this.underlying[index] = value; }

        public int Count => this.underlying.Count;

        public bool IsReadOnly => false;

        public void Add(IndexedZiPatchPartLocator item) => this.underlying.Add(item);

        public void Clear() => this.underlying.Clear();

        public bool Contains(IndexedZiPatchPartLocator item) => this.underlying.Contains(item);

        public void CopyTo(IndexedZiPatchPartLocator[] array, int arrayIndex) => this.underlying.CopyTo(array, arrayIndex);

        public IEnumerator<IndexedZiPatchPartLocator> GetEnumerator() => this.underlying.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.underlying.GetEnumerator();

        public int IndexOf(IndexedZiPatchPartLocator item) => this.underlying.IndexOf(item);

        public void Insert(int index, IndexedZiPatchPartLocator item) => this.underlying.Insert(index, item);

        public bool Remove(IndexedZiPatchPartLocator item) => this.underlying.Remove(item);

        public void RemoveAt(int index) => this.underlying.RemoveAt(index);

        public long FileSize => this.underlying.Count > 0 ? this.underlying.Last().TargetEnd : 0;

        public int BinarySearchByTargetOffset(long targetOffset)
        {
            return this.underlying.BinarySearch(new IndexedZiPatchPartLocator { TargetOffset = targetOffset }); ;
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
            else if (i == 0 && this.underlying.Count == 0)
            {
                this.underlying.Add(new IndexedZiPatchPartLocator
                {
                    TargetSize = offset,
                    TargetIndex = targetFileIndex,
                    SourceIndex = IndexedZiPatchPartLocator.SOURCE_INDEX_ZEROS,
                });
            }
            else if (i == this.underlying.Count && this.underlying[i - 1].TargetEnd == offset)
            {
                // Do nothing; split at TargetEnd of last part is give
            }
            else if (i == this.underlying.Count && this.underlying[i - 1].TargetEnd < offset)
            {
                this.underlying.Add(new IndexedZiPatchPartLocator
                {
                    TargetOffset = this.underlying[i - 1].TargetEnd,
                    TargetSize = offset - this.underlying[i - 1].TargetEnd,
                    TargetIndex = targetFileIndex,
                    SourceIndex = IndexedZiPatchPartLocator.SOURCE_INDEX_ZEROS,
                });
            }
            else
            {
                i -= 1;
                var part = this.underlying[i];

                if (part.IsDeflatedBlockData || part.IsEmptyBlock)
                {
                    this.underlying[i] = new IndexedZiPatchPartLocator
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
                    this.underlying.Insert(i + 1, new IndexedZiPatchPartLocator
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

                    this.underlying[i] = new IndexedZiPatchPartLocator
                    {
                        TargetOffset = part.TargetOffset,
                        TargetSize = offset - part.TargetOffset,
                        TargetIndex = targetFileIndex,
                        SourceIndex = part.SourceIndex,
                        SourceOffset = part.SourceOffset,
                        Crc32OrPlaceholderEntryDataUnits = part.Crc32OrPlaceholderEntryDataUnits,
                    };
                    this.underlying.Insert(i + 1, new IndexedZiPatchPartLocator
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

            if (left == this.underlying.Count)
            {
                this.underlying.Add(part);
                return;
            }

            var right = BinarySearchByTargetOffset(part.TargetEnd);
            if (right < 0)
                right = ~right;

            if (right - left - 1 < 0)
                Debugger.Break();

            this.underlying[left] = part;
            this.underlying.RemoveRange(left + 1, right - left - 1);
        }

        public async Task CalculateCrc32(List<Stream> sources, CancellationToken? cancellationToken = null)
        {
            await Task.Run(() =>
            {
                var list = this.underlying.ToArray();
                for (var i = 0; i < list.Length; ++i)
                {
                    if (cancellationToken.HasValue)
                        cancellationToken.Value.ThrowIfCancellationRequested();
                    if (list[i].IsFromSourceFile)
                        IndexedZiPatchPartLocator.CalculateCrc32(ref list[i], sources[list[i].SourceIndex]);
                }
                this.underlying.Clear();
                this.underlying.AddRange(list);
            });
        }

        public Stream ToStream(List<Stream> sources)
        {
            return new IndexedZiPatchTargetViewStream(sources, this);
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(RelativePath);
            writer.Write(this.underlying.Count);
            foreach (var item in this.underlying)
                item.WriteTo(writer);
        }

        public void ReadFrom(BinaryReader reader)
        {
            RelativePath = reader.ReadString();
            var dest = new IndexedZiPatchPartLocator[reader.ReadInt32()];
            for (var i = 0; i < dest.Length; ++i)
                dest[i].ReadFrom(reader);
            this.underlying.Clear();
            this.underlying.AddRange(dest);
        }
    }
}