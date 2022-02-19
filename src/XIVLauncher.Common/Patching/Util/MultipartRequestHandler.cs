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
                BaseStream = new BufferedStream(BaseStream, 1 << 22); // 4MB
            }

            if (MultipartBoundary == null)
            {
                switch (Response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        NoMoreParts = true;
                        return new ForwardSeekStream(new List<Tuple<Stream, long>>() {
                            Tuple.Create(BaseStream, (long)Response.Content.Headers.ContentLength)
                        }, (long)Response.Content.Headers.ContentLength, 0, (long)Response.Content.Headers.ContentLength);

                    case System.Net.HttpStatusCode.PartialContent:
                        if (Response.Content.Headers.ContentType.MediaType.ToLowerInvariant() != "multipart/byteranges")
                        {
                            NoMoreParts = true;
                            var rangeHeader = Response.Content.Headers.ContentRange;
                            return new ForwardSeekStream(new List<Tuple<Stream, long>>() {
                                Tuple.Create(BaseStream, (long)rangeHeader.To + 1 - (long)rangeHeader.From)
                            }, (long)rangeHeader.Length, (long)rangeHeader.From, (long)rangeHeader.To + 1);
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

                for (int i = 0, i_ = (int)MultipartBufferStream.Length - 1; i < i_; ++i)
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
                    var rangeFrom = (long)rangeHeader.From;
                    var rangeLength = (long)rangeHeader.To - rangeFrom + 1;

                    var dataBuffer = new byte[Math.Min(MultipartBufferStream.Length, rangeLength)];
                    MultipartBufferStream.Consume(dataBuffer, 0, dataBuffer.Length);

                    if (rangeLength == dataBuffer.Length)
                        return LastPartialStream = new ForwardSeekStream(new List<Tuple<Stream, long>>() {
                            Tuple.Create<Stream, long>(new MemoryStream(dataBuffer), dataBuffer.Length),
                        }, (long)rangeHeader.Length, rangeFrom, rangeFrom + rangeLength);
                    else
                        return LastPartialStream = new ForwardSeekStream(new List<Tuple<Stream, long>>() {
                            Tuple.Create<Stream, long>(new MemoryStream(dataBuffer), dataBuffer.Length),
                            Tuple.Create(BaseStream, rangeLength - dataBuffer.Length),
                        }, (long)rangeHeader.Length, rangeFrom, rangeFrom + rangeLength);
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

            private readonly List<StreamInfo> BaseStreams;  // <stream, use size>
            public readonly long TotalLength;
            public readonly long AvailableFromOffset;
            public readonly long AvailableToOffset;

            private long CurrentPosition;
            private int CurrentStreamIndex = 0;

            private readonly CircularMemoryStream BackwardSeekBuffer = new(CircularMemoryStream.FeedOverflowMode.DiscardOldest);
            private int BackwardDistance = 0;

            public ForwardSeekStream(List<Tuple<Stream, long>> baseStreams, long totalLength, long availableFromOffset, long availableToOffset)
            {
                BaseStreams = baseStreams.Select(x => new StreamInfo(x.Item1, x.Item2)).ToList();
                TotalLength = totalLength;
                CurrentPosition = AvailableFromOffset = availableFromOffset;
                AvailableToOffset = availableToOffset;
            }

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
                var totalRead = 0;
                if (BackwardDistance > 0)
                {
                    var read = Math.Min(count, BackwardDistance);
                    BackwardSeekBuffer.Seek(BackwardSeekBuffer.Length - BackwardDistance, SeekOrigin.Begin);
                    BackwardSeekBuffer.Read(buffer, offset, read);
                    count -= read;
                    BackwardDistance -= read;
                    offset += read;
                    totalRead += read;
                    CurrentPosition += read;
                }

                if (CurrentStreamIndex >= BaseStreams.Count)
                    return totalRead;

                while (count > 0 && CurrentStreamIndex < BaseStreams.Count)
                {
                    var streamSet = BaseStreams[CurrentStreamIndex];
                    var read = streamSet.Stream.Read(buffer, offset, (int)Math.Min(count, streamSet.Remaining));
                    if (read == 0)
                        throw new IOException("Read failure");

                    BackwardSeekBuffer.Feed(buffer, offset, read);

                    streamSet.Position += read;
                    CurrentPosition += read;
                    offset += read;
                    totalRead += read;
                    count -= read;
                    if (streamSet.Remaining == 0)
                        CurrentStreamIndex++;
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
                    if (BackwardDistance - offset > BackwardSeekBuffer.Length)
                        throw new ArgumentException($"Cannot seek backwards past {BackwardSeekBuffer.Length} bytes; tried to seek {-offset} bytes behind");
                    BackwardDistance -= (int)offset;
                    CurrentPosition += offset;
                    offset = 0;
                }
                if (BackwardDistance > 0 && offset > 0)
                {
                    var backwardCancelDistance = (int)Math.Min(BackwardDistance, offset);
                    BackwardDistance -= backwardCancelDistance;
                    CurrentPosition += backwardCancelDistance;
                    offset -= backwardCancelDistance;
                }
                if (offset == 0 || CurrentStreamIndex == BaseStreams.Count)
                    return CurrentPosition;

                while (CurrentStreamIndex < BaseStreams.Count && offset > 0)
                {
                    var streamSet = BaseStreams[CurrentStreamIndex];
                    var advanceOffset = Math.Min(offset, streamSet.Remaining);
                    CurrentPosition += advanceOffset;
                    streamSet.Position += advanceOffset;
                    offset -= advanceOffset;

                    if (streamSet.Stream.CanSeek && advanceOffset > BackwardSeekBuffer.Capacity)
                    {
                        streamSet.Stream.Seek(advanceOffset - BackwardSeekBuffer.Capacity, SeekOrigin.Current);
                        advanceOffset -= BackwardSeekBuffer.Capacity;
                    }

                    using var buffer = ReusableByteBufferManager.GetBuffer();
                    for (var i = 0; i < advanceOffset;)
                    {
                        var read = streamSet.Stream.Read(buffer.Buffer, 0, (int)Math.Min(advanceOffset - i, buffer.Buffer.Length));
                        if (read == 0)
                            throw new EndOfStreamException("Reached premature EOF");
                        i += read;
                        BackwardSeekBuffer.Feed(buffer.Buffer, 0, read);
                    }

                    if (streamSet.Remaining == 0)
                        CurrentStreamIndex++;
                }

                return CurrentPosition;
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}