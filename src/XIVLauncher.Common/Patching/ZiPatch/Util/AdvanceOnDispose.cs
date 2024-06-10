using System;
using System.IO;

namespace XIVLauncher.Common.Patching.ZiPatch.Util;

public sealed class AdvanceOnDispose : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly bool _forceRead;
    public readonly long OffsetBefore;
    public readonly long OffsetAfter;

    public AdvanceOnDispose(BinaryReader reader, long size, bool forceRead)
    {
        _reader = reader;
        _forceRead = forceRead;
        OffsetBefore = _reader.BaseStream.Position;
        OffsetAfter = OffsetBefore + size;
    }

    public long NumBytesRemaining => OffsetAfter - _reader.BaseStream.Position;

    public void Dispose()
    {
        if (_forceRead)
        {
            _ = _reader.ReadBytes((int)this.NumBytesRemaining);
            return;
        }

        _reader.BaseStream.Position = OffsetAfter;
    }
}
