using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.Util
{
    public class MultipartRequestHandler : IDisposable
    {
        private readonly HttpResponseMessage Response;
        private bool NoMoreParts = false;
        private Stream BaseStream;
        private string MultipartBoundary;
        private CircularMemoryStream MultipartBufferStream;
        private List<string> MultipartHeaderLines;
        private ForwardSeekStream LastPartialStream;
        private ReusableByteBufferManager.Allocation PartStreamBeginningBuffer = null;

        public MultipartRequestHandler(HttpResponseMessage responseMessage)
        {
            Response = responseMessage;
        }

        public async Task<ForwardSeekStream> NextPart(CancellationToken? cancellationToken = null)
        {
            if (NoMoreParts)
                return null;

            if (BaseStream == null)
            {
                BaseStream = await Response.Content.ReadAsStreamAsync();
            }

            if (MultipartBoundary == null)
            {
                switch (Response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        NoMoreParts = true;
                        return new ForwardSeekStream(Response.Content.Headers.ContentLength.Value, 0, Response.Content.Headers.ContentLength.Value)
                            .WithAppendStream(BaseStream, Response.Content.Headers.ContentLength.Value);

                    case System.Net.HttpStatusCode.PartialContent:
                        if (Response.Content.Headers.ContentType.MediaType.ToLowerInvariant() != "multipart/byteranges")
                        {
                            NoMoreParts = true;
                            var rangeHeader = Response.Content.Headers.ContentRange;
                            return new ForwardSeekStream(rangeHeader.Length.Value, rangeHeader.From.Value, rangeHeader.To.Value + 1)
                                .WithAppendStream(BaseStream, rangeHeader.To.Value + 1 - rangeHeader.From.Value);
                        }
                        else
                        {
                            MultipartBoundary = Response.Content.Headers.ContentType.Parameters.Where(p => p.Name.ToLowerInvariant() == "boundary").First().Value;
                            MultipartBufferStream = new();
                            MultipartHeaderLines = new();
                        }
                        break;

                    default:
                        Response.EnsureSuccessStatusCode();
                        throw new EndOfStreamException($"Unhandled success status code {Response.StatusCode}");
                }
            }

            if (LastPartialStream != null)
            {
                LastPartialStream.Seek(LastPartialStream.AvailableToOffset, SeekOrigin.Begin);
                LastPartialStream = null;
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
                        using var lineBuffer = ReusableByteBufferManager.GetBuffer();
                        if (i > lineBuffer.Buffer.Length)
                            throw new IOException($"Multipart header line is too long ({i} bytes)");

                        MultipartBufferStream.Consume(lineBuffer.Buffer, 0, i + 2);
                        MultipartHeaderLines.Add(Encoding.UTF8.GetString(lineBuffer.Buffer, 0, (int)i));
                    }
                    i = -1;

                    if (MultipartHeaderLines.Count == 0)
                        continue;
                    if (MultipartHeaderLines.Last() == $"--{MultipartBoundary}--")
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
                    
                    LastPartialStream = new ForwardSeekStream(rangeHeader.Length.Value, rangeFrom, rangeFrom + rangeLength);

                    var immediatelyAvailable = (int)Math.Min(MultipartBufferStream.Length, rangeLength);
                    if (immediatelyAvailable > 0)
                    {
                        if (PartStreamBeginningBuffer == null || PartStreamBeginningBuffer.Buffer.Length < immediatelyAvailable)
                        {
                            PartStreamBeginningBuffer?.Dispose();
                            PartStreamBeginningBuffer = ReusableByteBufferManager.GetBuffer(immediatelyAvailable);
                        }
                        MultipartBufferStream.Consume(PartStreamBeginningBuffer.Buffer, 0, immediatelyAvailable);
                        LastPartialStream.WithAppendStream(new MemoryStream(PartStreamBeginningBuffer.Buffer, 0, immediatelyAvailable));
                    }

                    if (immediatelyAvailable < rangeLength)
                        LastPartialStream.WithAppendStream(BaseStream, rangeLength - immediatelyAvailable);

                    return LastPartialStream;
                }

                if (eof && !NoMoreParts)
                    throw new EndOfStreamException("Reached premature EOF");
            }
        }

        public void Dispose()
        {
            PartStreamBeginningBuffer?.Dispose();
            MultipartBufferStream?.Dispose();
            BaseStream?.Dispose();
            Response?.Dispose();
        }

        public class ForwardSeekStream : Stream
        {
            private class StreamInfo
            {
                public readonly Stream Stream;
                public readonly long Length;
                public long Position;

                public StreamInfo(Stream stream, long length)
                {
                    Stream = stream;
                    Length = length;
                    Position = 0;
                }

                public long Remaining => Length - Position;
            }

            private readonly List<StreamInfo> BaseStreams = new();
            public readonly long TotalLength;
            public readonly long AvailableFromOffset;
            public readonly long AvailableToOffset;

            private long CurrentPosition;
            private int CurrentStreamIndex = 0;

            private const int BufferSize = 65536;
            private readonly CircularMemoryStream BackwardSeekBuffer = new(BufferSize, CircularMemoryStream.FeedOverflowMode.DiscardOldest);
            private readonly CircularMemoryStream ForwardBuffer = new(BufferSize, CircularMemoryStream.FeedOverflowMode.Throw);

            public ForwardSeekStream(long totalLength, long availableFromOffset, long availableToOffset)
            {
                TotalLength = totalLength;
                CurrentPosition = AvailableFromOffset = availableFromOffset;
                AvailableToOffset = availableToOffset;
            }

            public ForwardSeekStream WithAppendStream(Stream stream)
            {
                return WithAppendStream(stream, stream.Length);
            }

            public ForwardSeekStream WithAppendStream(Stream stream, long length)
            {
                if (length > 0)
                    BaseStreams.Add(new StreamInfo(stream, length));
                return this;
            }

            public long Remaining => BaseStreams.Select(x => x.Remaining).Sum();

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                BackwardSeekBuffer.Dispose();
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => TotalLength;

            public override long Position
            {
                get => CurrentPosition;
                set => Seek(value, SeekOrigin.Begin);
            }

            public override void Flush() => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (count == 0)
                    return 0;

                var totalRead = 0;
                if (BackwardSeekBuffer.Position < BackwardSeekBuffer.Length)
                {
                    var read = (int)Math.Min(count, BackwardSeekBuffer.Length - BackwardSeekBuffer.Position);
                    if (buffer == null)
                        BackwardSeekBuffer.Position += read;
                    else
                        BackwardSeekBuffer.Read(buffer, offset, read);
                    count -= read;
                    offset += read;
                    totalRead += read;
                    CurrentPosition += read;
                }

                if (count == 0)
                    return totalRead;

                using var buf2 = ReusableByteBufferManager.GetBuffer(BufferSize);
                while (count > 0)
                {
                    int read;
                    while (ForwardBuffer.Length == 0 && CurrentStreamIndex < BaseStreams.Count)
                    {
                        var streamSet = BaseStreams[CurrentStreamIndex];
                        read = streamSet.Stream.Read(buf2.Buffer, 0, (int)Math.Min(buf2.Buffer.Length, streamSet.Remaining));
                        if (read == 0)
                            throw new IOException("Read failure");

                        streamSet.Position += read;
                        ForwardBuffer.Feed(buf2.Buffer, 0, read);

                        if (streamSet.Remaining == 0)
                            CurrentStreamIndex++;
                    }

                    read = ForwardBuffer.Consume(buf2.Buffer, 0, Math.Min(buf2.Buffer.Length, count));
                    if (read == 0)
                        break;

                    BackwardSeekBuffer.Feed(buf2.Buffer, 0, read);
                    BackwardSeekBuffer.Position = BackwardSeekBuffer.Length;
                    if (buffer != null)
                        Array.Copy(buf2.Buffer, 0, buffer, offset, read);
                    CurrentPosition += read;
                    offset += read;
                    totalRead += read;
                    count -= read;
                }

                return totalRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (origin == SeekOrigin.Begin)
                    offset -= CurrentPosition;
                else if (origin == SeekOrigin.End)
                    offset = TotalLength - offset - CurrentPosition;

                if (offset < 0)
                {
                    if (BackwardSeekBuffer.Position + offset < 0)
                        throw new ArgumentException($"Cannot seek backwards past {BackwardSeekBuffer.Position} bytes; tried to seek {-offset} bytes behind");
                    BackwardSeekBuffer.Position += offset;
                    CurrentPosition += offset;
                    offset = 0;
                }
                if (BackwardSeekBuffer.Position < BackwardSeekBuffer.Length && offset > 0)
                {
                    var backwardCancelDistance = (int)Math.Min(BackwardSeekBuffer.Length - BackwardSeekBuffer.Position, offset);
                    BackwardSeekBuffer.Position += offset;
                    CurrentPosition += backwardCancelDistance;
                    offset -= backwardCancelDistance;
                }
                if (offset == 0)
                    return CurrentPosition;

                var read = Read(null, 0, (int)offset);
                if (read < offset)
                    throw new EndOfStreamException($"Reached premature EOF (wanted {offset} bytes, read {read} bytes)");

                return CurrentPosition;
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}