using System;
using System.Collections.Generic;
using System.IO;

#nullable enable

namespace XIVLauncher.Common.Patching.IndexedZiPatch;

public class IndexedZiPatchTargetViewStream : Stream
{
    private readonly List<Stream> sources;
    private readonly IndexedZiPatchTargetFile partList;
    private readonly bool disposeStreams;

    internal IndexedZiPatchTargetViewStream(List<Stream> sources, IndexedZiPatchTargetFile partList, bool disposeStreams)
    {
        this.sources = sources;
        this.partList = partList;
        this.disposeStreams = disposeStreams;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => this.partList.FileSize;

    public override long Position { get; set; }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposeStreams)
        {
            foreach (var s in sources)
                s.Dispose();
            this.sources.Clear();
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var beginOffset = offset;

        while (count > 0 && Position < Length)
        {
            var i = this.partList.BinarySearchByTargetOffset(Position);
            if (i < 0)
                i = ~i - 1;

            var reconstructedLength = this.partList[i].Reconstruct(this.sources, buffer, offset, count, (int)(Position - this.partList[i].TargetOffset));
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
