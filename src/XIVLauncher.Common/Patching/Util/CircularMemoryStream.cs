using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Patching.Util
{
    public class CircularMemoryStream : Stream
    {
        public enum FeedOverflowMode
        {
            ExtendCapacity,
            DiscardOldest,
            Throw,
        }

        private readonly FeedOverflowMode OverflowMode;
        private ReusableByteBufferManager.Allocation ReusableBuffer;
        private int BufferValidTo = 0;
        private int BufferValidFrom = 0;
        private bool Empty = true;
        private int ExternalPosition = 0;

        public CircularMemoryStream(int baseCapacity = 0, FeedOverflowMode feedOverflowMode = FeedOverflowMode.ExtendCapacity)
        {
            OverflowMode = feedOverflowMode;
            if (feedOverflowMode == FeedOverflowMode.ExtendCapacity && baseCapacity == 0)
                ReusableBuffer = ReusableByteBufferManager.GetBuffer();
            else
                ReusableBuffer = ReusableByteBufferManager.GetBuffer(baseCapacity);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            ReusableBuffer?.Dispose();
        }

        public void Reserve(long capacity)
        {
            if (capacity <= Capacity)
                return;

            var length = (int)Length;
            var newBuffer = ReusableByteBufferManager.GetBuffer(capacity);
            if (!Empty)
            {
                if (BufferValidFrom < BufferValidTo)
                    Array.Copy(ReusableBuffer.Buffer, BufferValidFrom, newBuffer.Buffer, 0, length);
                else
                {
                    Array.Copy(ReusableBuffer.Buffer, BufferValidFrom, newBuffer.Buffer, 0, Capacity - BufferValidFrom);
                    Array.Copy(ReusableBuffer.Buffer, 0, newBuffer.Buffer, Capacity - BufferValidFrom, BufferValidTo);
                }
            }

            ReusableBuffer.Dispose();
            ReusableBuffer = newBuffer;

            BufferValidFrom = 0;
            BufferValidTo = length;
        }

        public void Feed(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;
            if (Length + count > Capacity)
            {
                switch (OverflowMode)
                {
                    case FeedOverflowMode.ExtendCapacity:
                        Reserve(Length + count);
                        break;

                    case FeedOverflowMode.DiscardOldest:
                        if (count >= Capacity)
                        {
                            BufferValidFrom = 0;
                            BufferValidTo = 0;
                            Array.Copy(buffer, offset + count - Capacity, ReusableBuffer.Buffer, 0, Capacity);
                            ExternalPosition = 0;
                            Empty = false;
                            return;
                        }
                        else
                        {
                            var keepCount = Capacity - count;
                            ExternalPosition = (int)Math.Max(0, ExternalPosition - (Length - keepCount));
                            BufferValidFrom = (BufferValidTo - keepCount + Capacity) % Capacity;
                        }
                        break;

                    case FeedOverflowMode.Throw:
                        throw new InvalidOperationException($"Cannot feed {count} bytes (length={Length}, capacity={Capacity})");
                }
            }
            if (BufferValidTo + count < Capacity)
            {
                Array.Copy(buffer, offset, ReusableBuffer.Buffer, BufferValidTo, count);
                BufferValidTo = (BufferValidTo + count) % Capacity;
            }
            else
            {
                var feedLength1 = Capacity - BufferValidTo;
                var feedLength2 = count - feedLength1;
                Array.Copy(buffer, offset, ReusableBuffer.Buffer, BufferValidTo, feedLength1);
                Array.Copy(buffer, offset + feedLength1, ReusableBuffer.Buffer, 0, feedLength2);
                BufferValidTo = feedLength2 % Capacity;
            }
            Empty = false;
        }

        public int Consume(byte[] buffer, int offset, int count, bool peek = false)
        {
            count = Math.Min(count, (int)Length);
            if (buffer != null && count > 0)
            {
                if (BufferValidFrom < BufferValidTo)
                    Array.Copy(ReusableBuffer.Buffer, BufferValidFrom, buffer, offset, count);
                else
                {
                    var consumeCount1 = Math.Min(count, Capacity - BufferValidFrom);
                    var consumeCount2 = count - consumeCount1;
                    Array.Copy(ReusableBuffer.Buffer, BufferValidFrom, buffer, offset, consumeCount1);
                    Array.Copy(ReusableBuffer.Buffer, 0, buffer, offset + consumeCount1, consumeCount2);
                }
            }
            if (!peek)
            {
                BufferValidFrom = (BufferValidFrom + count) % Capacity;
                if (count > 0 && BufferValidFrom == BufferValidTo)
                {
                    BufferValidFrom = BufferValidTo = 0;
                    Empty = true;
                }
                ExternalPosition = Math.Max(0, ExternalPosition - count);
            }
            return count;
        }

        public byte this[long i]
        {
            get
            {
                if (i < 0 || i >= Length)
                    throw new ArgumentOutOfRangeException(nameof(i));
                return ReusableBuffer.Buffer[(BufferValidFrom + i) % Capacity];
            }
            set
            {
                if (i < 0 || i >= Length)
                    throw new ArgumentOutOfRangeException(nameof(i));
                ReusableBuffer.Buffer[(BufferValidFrom + i) % Capacity] = value;
            }
        }

        public int Capacity => ReusableBuffer.Buffer.Length;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => (BufferValidTo == BufferValidFrom && !Empty) ? Capacity : ((BufferValidTo - BufferValidFrom + Capacity) % Capacity);
        public override long Position
        {
            get => ExternalPosition;
            set => ExternalPosition = (int)value;
        }

        public override void Flush() { }

        public override void SetLength(long value)
        {
            if (value > int.MaxValue)
                throw new ArgumentOutOfRangeException("Length can be up to int.MaxValue");
            Empty = value == 0;
            if (Empty)
            {
                BufferValidFrom = BufferValidTo = 0;
                return;
            }

            var intValue = (int)value;
            if (intValue > Capacity)
                Reserve(intValue);
            else if (intValue > Length)
            {
                var extendLength = (int)(intValue - Length);
                var newValidTo = (BufferValidTo + extendLength) % Capacity;

                if (BufferValidTo < newValidTo)
                    Array.Clear(ReusableBuffer.Buffer, BufferValidTo, newValidTo - BufferValidTo);
                else
                {
                    Array.Clear(ReusableBuffer.Buffer, BufferValidTo, Capacity - BufferValidTo);
                    Array.Clear(ReusableBuffer.Buffer, 0, newValidTo);
                }

                BufferValidTo = newValidTo;
            }
            else if (intValue < Length)
                BufferValidTo = (BufferValidFrom + intValue) % Capacity;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = Math.Min(count, (int)Length - ExternalPosition);

            var readOffset = (BufferValidFrom + ExternalPosition) % Capacity;
            if (readOffset + count <= Capacity)
                Array.Copy(ReusableBuffer.Buffer, readOffset, buffer, offset, count);
            else
            {
                var readCount1 = Capacity - readOffset;
                var readCount2 = count - readCount1;
                Array.Copy(ReusableBuffer.Buffer, readOffset, buffer, offset, readCount1);
                Array.Copy(ReusableBuffer.Buffer, 0, buffer, offset + readCount1, readCount2);
            }
            ExternalPosition += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = ExternalPosition;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition += offset;
                    break;
                case SeekOrigin.End:
                    newPosition = Length - offset;
                    break;
            }
            if (newPosition < 0)
                throw new ArgumentException("Seeking is attempted before the beginning of the stream.");
            if (newPosition > Length)
                newPosition = Length;
            ExternalPosition = (int)newPosition;
            return newPosition;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Length + count > Capacity)
                Reserve((int)(Length + count));

            var writeOffset = (BufferValidFrom + ExternalPosition) % Capacity;
            if (writeOffset + count <= Capacity)
                Array.Copy(buffer, offset, ReusableBuffer.Buffer, writeOffset, count);
            else
            {
                var writeCount1 = Capacity - writeOffset;
                var writeCount2 = count - writeCount1;
                Array.Copy(buffer, offset, ReusableBuffer.Buffer, writeOffset, writeCount1);
                Array.Copy(buffer, offset + writeCount1, ReusableBuffer.Buffer, 0, writeCount2);
            }
            ExternalPosition += count;
        }
    }
}
