using System;
using System.Collections.Generic;
using System.IO;

namespace XIVLauncher.Common.Patching.IndexedZiPatch
{
    public class IndexedZiPatchTargetViewStream : Stream
    {
        private readonly List<Stream> Sources;
        private readonly IndexedZiPatchTargetFile PartList;

        internal IndexedZiPatchTargetViewStream(List<Stream> sources, IndexedZiPatchTargetFile partList)
        {
            Sources = sources;
            PartList = partList;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => PartList.FileSize;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var beginOffset = offset;
            while (count > 0 && Position < Length)
            {
                var i = PartList.BinarySearchByTargetOffset(Position);
                if (i < 0)
                    i = ~i - 1;

                var reconstructedLength = PartList[i].Reconstruct(Sources, buffer, offset, count, (int)(Position - PartList[i].TargetOffset));
                offset += reconstructedLength;
                count -= reconstructedLength;
                Position += reconstructedLength;
            }
            return offset - beginOffset;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var position = Position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;

                case SeekOrigin.Current:
                    position += offset;
                    break;

                case SeekOrigin.End:
                    position = Length - offset;
                    break;

                default:
                    throw new NotImplementedException();
            }

            if (position < 0)
                throw new ArgumentException("Seeking is attempted before the beginning of the stream.");

            Position = Math.Min(position, Length);

            return Position;
        }

        public override void Flush() => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
