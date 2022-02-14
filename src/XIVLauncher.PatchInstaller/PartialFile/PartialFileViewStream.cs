using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PartialFile
{
    public class PartialFileViewStream : Stream
    {
        private readonly List<Stream> Sources;
        private readonly PartialFilePartList Parts;

        public PartialFileViewStream(List<Stream> sources, PartialFilePartList parts)
        {
            Sources = sources;
            Parts = parts;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => Parts.Count == 0 ? 0 : Parts[Parts.Count - 1].TargetEnd;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var beginOffset = offset;

            byte[] targetData = null;

            while (count > 0 && Position < Length)
            {
                var i = Parts.BinarySearchByTargetOffset(Position);
                if (i < 0)
                    i = ~i - 1;

                var part = Parts[i];
                var relativeOffset = Position - part.TargetOffset;
                var targetLength = (int)Math.Min(part.TargetSize, count);

                if (part.IsUnavailable)
                {
                    throw new InvalidOperationException("Unavailable part read attempt");
                }
                else if (part.IsAllZeros)
                {
                    Array.Clear(buffer, offset, targetLength);
                }
                else if (part.IsEmptyBlock)
                {
                    Array.Clear(buffer, offset, targetLength);

                    if (relativeOffset < 16)
                    {
                        if (targetData == null)
                            targetData = new byte[16000];
                        var tmp = new MemoryStream(targetData);
                        using (var writer = new BinaryWriter(tmp))
                        {
                            writer.Write(1 << 7);
                            writer.Write(0);
                            writer.Write(0);
                            writer.Write((part.SourceSize >> 7) - 1);
                        }
                        Array.Copy(targetData, relativeOffset, buffer, offset, 16 - relativeOffset);
                    }
                }
                else if (part.SourceIsDeflated)
                {
                    var source = Sources[part.SourceIndex];
                    
                    var sourceData = new byte[part.SourceSize];
                    source.Seek(part.SourceOffset, SeekOrigin.Begin);
                    if (sourceData.Length != source.Read(sourceData, 0, sourceData.Length))
                        throw new IOException("Failed to read full part of source file");

                    if (targetData == null)
                        targetData = new byte[16000];
                    using (var stream = new DeflateStream(new MemoryStream(sourceData), CompressionMode.Decompress))
                        stream.Read(targetData, 0, targetData.Length);
                    Array.Copy(targetData, part.SplitDecodedSourceFrom, buffer, offset, part.TargetSize);
                }
                else
                {
                    var source = Sources[part.SourceIndex];
                    source.Seek(part.SourceOffset + part.SplitDecodedSourceFrom + relativeOffset, SeekOrigin.Begin);
                    if (targetLength != source.Read(buffer, offset, targetLength))
                        throw new IOException("Failed to read full part of source file");
                }

                offset += targetLength;
                count -= targetLength;
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
                    position -= offset;
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
