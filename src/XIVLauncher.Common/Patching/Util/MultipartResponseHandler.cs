using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.Util
{
    public class MultipartResponseHandler : IDisposable
    {
        private readonly HttpResponseMessage Response;
        private bool NoMoreParts = false;
        private Stream BaseStream;
        private string MultipartBoundary;
        private string MultipartEndBoundary;
        private CircularMemoryStream MultipartBufferStream;
        private List<string> MultipartHeaderLines;

        public MultipartResponseHandler(HttpResponseMessage responseMessage)
        {
            Response = responseMessage;
        }

        public async Task<MultipartPartStream> NextPart(CancellationToken? cancellationToken = null)
        {
            if (NoMoreParts)
                return null;

            if (BaseStream == null)
                BaseStream = new BufferedStream(await Response.Content.ReadAsStreamAsync(), 16384);

            if (MultipartBoundary == null)
            {
                switch (Response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        {
                            NoMoreParts = true;
                            var stream = new MultipartPartStream(Response.Content.Headers.ContentLength.Value, 0, Response.Content.Headers.ContentLength.Value);
                            stream.AppendBaseStream(new ReadLengthLimitingStream(BaseStream, Response.Content.Headers.ContentLength.Value));
                            return stream;
                        }

                    case System.Net.HttpStatusCode.PartialContent:
                        if (Response.Content.Headers.ContentType.MediaType.ToLowerInvariant() != "multipart/byteranges")
                        {
                            NoMoreParts = true;
                            var rangeHeader = Response.Content.Headers.ContentRange;
                            var rangeLength = rangeHeader.To.Value + 1 - rangeHeader.From.Value;
                            var stream = new MultipartPartStream(rangeHeader.Length.Value, rangeHeader.From.Value, rangeLength);
                            stream.AppendBaseStream(new ReadLengthLimitingStream(BaseStream, rangeLength));
                            return stream;
                        }
                        else
                        {
                            MultipartBoundary = "--" + Response.Content.Headers.ContentType.Parameters.Where(p => p.Name.ToLowerInvariant() == "boundary").First().Value;
                            MultipartEndBoundary = MultipartBoundary + "--";
                            MultipartBufferStream = new();
                            MultipartHeaderLines = new();
                        }
                        break;

                    default:
                        Response.EnsureSuccessStatusCode();
                        throw new EndOfStreamException($"Unhandled success status code {Response.StatusCode}");
                }
            }

            while (true)
            {
                if (cancellationToken.HasValue)
                    cancellationToken.Value.ThrowIfCancellationRequested();

                var eof = false;
                using (var buffer = ReusableByteBufferManager.GetBuffer())
                {
                    int readSize;
                    if (cancellationToken == null)
                        readSize = await BaseStream.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length);
                    else
                        readSize = await BaseStream.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length, (CancellationToken)cancellationToken);

                    if (readSize == 0)
                        eof = true;
                    else
                        MultipartBufferStream.Feed(buffer.Buffer, 0, readSize);
                }

                for (int i = 0; i < MultipartBufferStream.Length - 1; ++i)
                {
                    if (MultipartBufferStream[i + 0] != '\r' || MultipartBufferStream[i + 1] != '\n')
                        continue;

                    var IsEmptyLine = i == 0;
                    if (IsEmptyLine)
                        MultipartBufferStream.Consume(null, 0, 2);
                    else
                    {
                        using var buffer = ReusableByteBufferManager.GetBuffer();
                        if (i > buffer.Buffer.Length)
                            throw new IOException($"Multipart header line is too long ({i} bytes)");

                        MultipartBufferStream.Consume(buffer.Buffer, 0, i + 2);
                        MultipartHeaderLines.Add(Encoding.UTF8.GetString(buffer.Buffer, 0, i));
                    }
                    i = -1;

                    if (MultipartHeaderLines.Count == 0)
                        continue;
                    if (MultipartHeaderLines.Last() == MultipartEndBoundary)
                    {
                        NoMoreParts = true;
                        return null;
                    }
                    if (!IsEmptyLine)
                        continue;

                    ContentRangeHeaderValue rangeHeader = null;
                    foreach (var headerLine in MultipartHeaderLines)
                    {
                        var kvs = headerLine.Split(new char[] { ':' }, 2);
                        if (kvs.Length != 2)
                            continue;
                        if (kvs[0].ToLowerInvariant() != "content-range")
                            continue;
                        if (ContentRangeHeaderValue.TryParse(kvs[1], out rangeHeader))
                            break;
                    }
                    if (rangeHeader == null)
                        throw new IOException("Content-Range not found in multipart part");

                    MultipartHeaderLines.Clear();
                    var rangeFrom = rangeHeader.From.Value;
                    var rangeLength = rangeHeader.To.Value - rangeFrom + 1;
                    var stream = new MultipartPartStream(rangeHeader.Length.Value, rangeFrom, rangeLength);
                    stream.AppendBaseStream(new ConsumeLengthLimitingStream(MultipartBufferStream, Math.Min(rangeLength, MultipartBufferStream.Length)));
                    stream.AppendBaseStream(new ReadLengthLimitingStream(BaseStream, stream.UnfulfilledBaseStreamLength));
                    return stream;
                }

                if (eof && !NoMoreParts)
                    throw new EndOfStreamException("Reached premature EOF");
            }
        }

        public void Dispose()
        {
            MultipartBufferStream?.Dispose();
            BaseStream?.Dispose();
            Response?.Dispose();
        }

        public class ReadLengthLimitingStream : Stream
        {
            private readonly Stream BaseStream;
            private readonly long LimitedLength;
            private long LimitedPointer = 0;

            public ReadLengthLimitingStream(Stream stream, long length)
            {
                BaseStream = stream;
                LimitedLength = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                count = (int)Math.Min(count, LimitedLength - LimitedPointer);
                if (count == 0)
                    return 0;

                var read = BaseStream.Read(buffer, offset, count);
                if (read == 0)
                    throw new EndOfStreamException("Premature end of stream detected");
                LimitedPointer += read;
                return read;
            }

            public override long Length => LimitedLength;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Position { get => LimitedPointer; set => throw new NotSupportedException(); }

            public override void Flush() => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        public class ConsumeLengthLimitingStream : Stream
        {
            private readonly CircularMemoryStream BaseStream;
            private readonly long LimitedLength;
            private long LimitedPointer = 0;

            public ConsumeLengthLimitingStream(CircularMemoryStream stream, long length)
            {
                BaseStream = stream;
                LimitedLength = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                count = (int)Math.Min(count, LimitedLength - LimitedPointer);
                if (count == 0)
                    return 0;

                var read = BaseStream.Consume(buffer, offset, count);
                if (read == 0)
                    throw new EndOfStreamException("Premature end of stream detected");
                LimitedPointer += read;
                return read;
            }

            public override long Length => LimitedLength;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Position { get => LimitedPointer; set => throw new NotSupportedException(); }

            public override void Flush() => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        public class MultipartPartStream : Stream
        {
            private readonly CircularMemoryStream LoopStream = new(16384, CircularMemoryStream.FeedOverflowMode.DiscardOldest);
            private readonly List<Stream> BaseStreams = new();
            private int BaseStreamIndex = 0;
            public readonly long OriginTotalLength;
            public readonly long OriginOffset;
            public readonly long OriginLength;
            public long OriginEnd => OriginOffset + OriginLength;
            private long PositionInternal;

            public MultipartPartStream(long originTotalLength, long originOffset, long originLength)
            {
                OriginTotalLength = originTotalLength;
                OriginOffset = originOffset;
                OriginLength = originLength;
                PositionInternal = originOffset;
            }

            public void AppendBaseStream(Stream stream)
            {
                if (stream.Length == 0)
                    return;
                if (UnfulfilledBaseStreamLength < stream.Length)
                    throw new ArgumentException("Total length of given streams exceed OriginTotalLength.");
                BaseStreams.Add(stream);
            }

            public long UnfulfilledBaseStreamLength => OriginLength - BaseStreams.Select(x => x.Length).Sum();

            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalRead = 0;
                while (count > 0 && LoopStream.Position < LoopStream.Length)
                {
                    var read1 = (int)Math.Min(count, LoopStream.Length - LoopStream.Position);
                    var read2 = LoopStream.Read(buffer, offset, read1);
                    if (read2 == 0)
                        throw new EndOfStreamException("MultipartPartStream.Read:1");

                    totalRead += read2;
                    PositionInternal += read2;
                    count -= read2;
                    offset += read2;
                }

                while (count > 0 && BaseStreamIndex < BaseStreams.Count)
                {
                    var stream = BaseStreams[BaseStreamIndex];
                    var read1 = (int)Math.Min(count, stream.Length - stream.Position);
                    var read2 = stream.Read(buffer, offset, read1);
                    if (read2 == 0)
                        throw new EndOfStreamException("MultipartPartStream.Read:2");

                    LoopStream.Feed(buffer, offset, read2);
                    LoopStream.Position = LoopStream.Length;

                    totalRead += read2;
                    PositionInternal += read2;
                    count -= read2;
                    offset += read2;

                    if (stream.Position == stream.Length)
                        BaseStreamIndex++;
                }

                return totalRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        offset -= PositionInternal;
                        break;
                    case SeekOrigin.End:
                        offset = OriginTotalLength - offset - PositionInternal;
                        break;
                }

                var finalPosition = PositionInternal + offset;

                if (finalPosition > OriginOffset + OriginLength)
                    throw new ArgumentException("Tried to seek after the end of the segment.");
                else if (finalPosition < OriginOffset)
                    throw new ArgumentException("Tried to seek behind the beginning of the segment.");

                var backwards = LoopStream.Length - LoopStream.Position;
                var backwardAdjustment = Math.Min(backwards, offset);
                LoopStream.Position += backwardAdjustment;  // This will throw if there are not enough old data available
                offset -= backwardAdjustment;
                PositionInternal += backwardAdjustment;

                if (offset > 0)
                {
                    using var buf = ReusableByteBufferManager.GetBuffer();
                    for (var i = 0; i < offset; i += buf.Buffer.Length)
                        if (0 == Read(buf.Buffer, 0, (int)Math.Min(offset - i, buf.Buffer.Length)))
                            throw new EndOfStreamException("MultipartPartStream.Read:3");
                }

                if (PositionInternal != finalPosition)
                    throw new IOException("Failed to seek properly.");

                return PositionInternal;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => OriginTotalLength;

            public override long Position { get => PositionInternal; set => Seek(value, SeekOrigin.Begin); }

            public override void Flush() => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}