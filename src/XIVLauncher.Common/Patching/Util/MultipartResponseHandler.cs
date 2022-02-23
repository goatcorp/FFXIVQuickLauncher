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
    public class MultipartResponseHandler : IDisposable
    {
        private readonly HttpResponseMessage response;
        private bool noMoreParts = false;
        private Stream baseStream;
        public string MultipartBoundary;
        private string multipartEndBoundary;
        private CircularMemoryStream multipartBufferStream;
        private List<string> multipartHeaderLines;

        public MultipartResponseHandler(HttpResponseMessage responseMessage)
        {
            this.response = responseMessage;
        }

        public async Task<MultipartPartStream> NextPart(CancellationToken? cancellationToken = null)
        {
            if (this.noMoreParts)
                return null;

            if (this.baseStream == null)
                this.baseStream = new BufferedStream(await this.response.Content.ReadAsStreamAsync(), 16384);

            if (MultipartBoundary == null)
            {
                switch (this.response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        {
                            this.noMoreParts = true;
                            var stream = new MultipartPartStream(this.response.Content.Headers.ContentLength.Value, 0, this.response.Content.Headers.ContentLength.Value);
                            stream.AppendBaseStream(new ReadLengthLimitingStream(this.baseStream, this.response.Content.Headers.ContentLength.Value));
                            return stream;
                        }

                    case System.Net.HttpStatusCode.PartialContent:
                        if (this.response.Content.Headers.ContentType.MediaType.ToLowerInvariant() != "multipart/byteranges")
                        {
                            this.noMoreParts = true;
                            var rangeHeader = this.response.Content.Headers.ContentRange;
                            var rangeLength = rangeHeader.To.Value + 1 - rangeHeader.From.Value;
                            var stream = new MultipartPartStream(rangeHeader.Length.Value, rangeHeader.From.Value, rangeLength);
                            stream.AppendBaseStream(new ReadLengthLimitingStream(this.baseStream, rangeLength));
                            return stream;
                        }
                        else
                        {
                            MultipartBoundary = "--" + this.response.Content.Headers.ContentType.Parameters.Where(p => p.Name.ToLowerInvariant() == "boundary").First().Value;
                            this.multipartEndBoundary = MultipartBoundary + "--";
                            this.multipartBufferStream = new();
                            this.multipartHeaderLines = new();
                        }
                        break;

                    default:
                        this.response.EnsureSuccessStatusCode();
                        throw new EndOfStreamException($"Unhandled success status code {this.response.StatusCode}");
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
                        readSize = await this.baseStream.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length);
                    else
                        readSize = await this.baseStream.ReadAsync(buffer.Buffer, 0, buffer.Buffer.Length, (CancellationToken)cancellationToken);

                    if (readSize == 0)
                        eof = true;
                    else
                        this.multipartBufferStream.Feed(buffer.Buffer, 0, readSize);
                }

                for (int i = 0; i < this.multipartBufferStream.Length - 1; ++i)
                {
                    if (this.multipartBufferStream[i + 0] != '\r' || this.multipartBufferStream[i + 1] != '\n')
                        continue;

                    var isEmptyLine = i == 0;
                    
                    if (isEmptyLine)
                        this.multipartBufferStream.Consume(null, 0, 2);
                    else
                    {
                        using var buffer = ReusableByteBufferManager.GetBuffer();
                        if (i > buffer.Buffer.Length)
                            throw new IOException($"Multipart header line is too long ({i} bytes)");

                        this.multipartBufferStream.Consume(buffer.Buffer, 0, i + 2);
                        this.multipartHeaderLines.Add(Encoding.UTF8.GetString(buffer.Buffer, 0, i));
                    }
                    i = -1;

                    if (this.multipartHeaderLines.Count == 0)
                        continue;
                    if (this.multipartHeaderLines.Last() == this.multipartEndBoundary)
                    {
                        this.noMoreParts = true;
                        return null;
                    }
                    if (!isEmptyLine)
                        continue;

                    ContentRangeHeaderValue rangeHeader = null;
                    foreach (var headerLine in this.multipartHeaderLines)
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

                    this.multipartHeaderLines.Clear();
                    var rangeFrom = rangeHeader.From.Value;
                    var rangeLength = rangeHeader.To.Value - rangeFrom + 1;
                    var stream = new MultipartPartStream(rangeHeader.Length.Value, rangeFrom, rangeLength);
                    stream.AppendBaseStream(new ConsumeLengthLimitingStream(this.multipartBufferStream, Math.Min(rangeLength, this.multipartBufferStream.Length)));
                    stream.AppendBaseStream(new ReadLengthLimitingStream(this.baseStream, stream.UnfulfilledBaseStreamLength));
                    return stream;
                }

                if (eof && !this.noMoreParts)
                    throw new EndOfStreamException("Reached premature EOF");
            }
        }

        public void Dispose()
        {
            this.multipartBufferStream?.Dispose();
            this.baseStream?.Dispose();
            this.response?.Dispose();
        }

        private class ReadLengthLimitingStream : Stream
        {
            private readonly Stream baseStream;
            private readonly long limitedLength;
            private long limitedPointer = 0;

            public ReadLengthLimitingStream(Stream stream, long length)
            {
                this.baseStream = stream;
                this.limitedLength = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                count = (int)Math.Min(count, this.limitedLength - this.limitedPointer);
                if (count == 0)
                    return 0;

                var read = this.baseStream.Read(buffer, offset, count);
                if (read == 0)
                    throw new EndOfStreamException("Premature end of stream detected");
                this.limitedPointer += read;
                return read;
            }

            public override long Length => this.limitedLength;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Position { get => this.limitedPointer; set => throw new NotSupportedException(); }

            public override void Flush() => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private class ConsumeLengthLimitingStream : Stream
        {
            private readonly CircularMemoryStream baseStream;
            private readonly long limitedLength;
            private long limitedPointer = 0;

            public ConsumeLengthLimitingStream(CircularMemoryStream stream, long length)
            {
                this.baseStream = stream;
                this.limitedLength = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                count = (int)Math.Min(count, this.limitedLength - this.limitedPointer);
                if (count == 0)
                    return 0;

                var read = this.baseStream.Consume(buffer, offset, count);
                if (read == 0)
                    throw new EndOfStreamException("Premature end of stream detected");
                this.limitedPointer += read;
                return read;
            }

            public override long Length => this.limitedLength;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Position { get => this.limitedPointer; set => throw new NotSupportedException(); }

            public override void Flush() => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        public class MultipartPartStream : Stream
        {
            private readonly CircularMemoryStream loopStream = new(16384, CircularMemoryStream.FeedOverflowMode.DiscardOldest);
            private readonly List<Stream> baseStreams = new();
            private int baseStreamIndex = 0;
            public readonly long OriginTotalLength;
            public readonly long OriginOffset;
            public readonly long OriginLength;
            public long OriginEnd => OriginOffset + OriginLength;
            private long positionInternal;

            internal MultipartPartStream(long originTotalLength, long originOffset, long originLength)
            {
                OriginTotalLength = originTotalLength;
                OriginOffset = originOffset;
                OriginLength = originLength;
                this.positionInternal = originOffset;
            }

            internal void AppendBaseStream(Stream stream)
            {
                if (stream.Length == 0)
                    return;
                if (UnfulfilledBaseStreamLength < stream.Length)
                    throw new ArgumentException("Total length of given streams exceed OriginTotalLength.");
                this.baseStreams.Add(stream);
            }

            internal long UnfulfilledBaseStreamLength => OriginLength - this.baseStreams.Select(x => x.Length).Sum();

            public void CaptureBackwards(long captureCapacity)
            {
                this.loopStream.Reserve(captureCapacity);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int totalRead = 0;
                while (count > 0 && this.loopStream.Position < this.loopStream.Length)
                {
                    var read1 = (int)Math.Min(count, this.loopStream.Length - this.loopStream.Position);
                    var read2 = this.loopStream.Read(buffer, offset, read1);
                    if (read2 == 0)
                        throw new EndOfStreamException("MultipartPartStream.Read:1");

                    totalRead += read2;
                    this.positionInternal += read2;
                    count -= read2;
                    offset += read2;
                }

                while (count > 0 && this.baseStreamIndex < this.baseStreams.Count)
                {
                    var stream = this.baseStreams[this.baseStreamIndex];
                    var read1 = (int)Math.Min(count, stream.Length - stream.Position);
                    var read2 = stream.Read(buffer, offset, read1);
                    if (read2 == 0)
                        throw new EndOfStreamException("MultipartPartStream.Read:2");

                    this.loopStream.Feed(buffer, offset, read2);
                    this.loopStream.Position = this.loopStream.Length;

                    totalRead += read2;
                    this.positionInternal += read2;
                    count -= read2;
                    offset += read2;

                    if (stream.Position == stream.Length)
                        this.baseStreamIndex++;
                }

                return totalRead;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        offset -= this.positionInternal;
                        break;
                    case SeekOrigin.End:
                        offset = OriginTotalLength - offset - this.positionInternal;
                        break;
                }

                var finalPosition = this.positionInternal + offset;

                if (finalPosition > OriginOffset + OriginLength)
                    throw new ArgumentException("Tried to seek after the end of the segment.");
                else if (finalPosition < OriginOffset)
                    throw new ArgumentException("Tried to seek behind the beginning of the segment.");

                var backwards = this.loopStream.Length - this.loopStream.Position;
                var backwardAdjustment = Math.Min(backwards, offset);
                this.loopStream.Position += backwardAdjustment;  // This will throw if there are not enough old data available
                offset -= backwardAdjustment;
                this.positionInternal += backwardAdjustment;

                if (offset > 0)
                {
                    using var buf = ReusableByteBufferManager.GetBuffer();
                    for (var i = 0; i < offset; i += buf.Buffer.Length)
                        if (0 == Read(buf.Buffer, 0, (int)Math.Min(offset - i, buf.Buffer.Length)))
                            throw new EndOfStreamException("MultipartPartStream.Read:3");
                }

                if (this.positionInternal != finalPosition)
                    throw new IOException("Failed to seek properly.");

                return this.positionInternal;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => OriginTotalLength;

            public override long Position { get => this.positionInternal; set => Seek(value, SeekOrigin.Begin); }

            public override void Flush() => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}