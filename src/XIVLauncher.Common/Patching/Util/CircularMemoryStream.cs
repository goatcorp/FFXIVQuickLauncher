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
        private const int CapacityGrowthUnit = 16384;
        public enum FeedOverflowMode
        {
            ExtendCapacity,
            DiscardOldest,
        }

        private readonly FeedOverflowMode OverflowMode;
        private ReusableByteBufferManager.Allocation ReusableBuffer;
        private byte[] Buffer;
        private int BufferValidTo = 0;
        private int BufferValidFrom = 0;
        private bool Empty = true;
        private int ExternalPosition = 0;

        public CircularMemoryStream(FeedOverflowMode feedOverflowMode = FeedOverflowMode.ExtendCapacity)
        {
            OverflowMode = feedOverflowMode;
            ReusableBuffer = ReusableByteBufferManager.GetBuffer(14);
            Buffer = ReusableBuffer.Buffer;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            ReusableBuffer?.Dispose();
        }

        public void Reserve(long capacity, bool truncate = false)
        {
            if (capacity < Capacity)
            {
                if (truncate)
                {
                    throw new NotImplementedException();
                }
                return;
            }
            if (capacity == Capacity)
                return;

            capacity = (capacity + CapacityGrowthUnit - 1) / CapacityGrowthUnit * CapacityGrowthUnit;
            var length = (int)Length;
            var newBuffer = new byte[capacity];
            if (!Empty)
            {
                if (BufferValidFrom < BufferValidTo)
                    Array.Copy(Buffer, BufferValidFrom, newBuffer, 0, length);
                else
                {
                    Array.Copy(Buffer, BufferValidFrom, newBuffer, 0, Capacity - BufferValidFrom);
                    Array.Copy(Buffer, 0, newBuffer, Capacity - BufferValidFrom, BufferValidTo);
                }
            }

            if (ReusableBuffer != null)
            {
                ReusableBuffer.Dispose();
                ReusableBuffer = null;
            }

            Buffer = newBuffer;
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
                            Array.Copy(buffer, offset + count - Capacity, Buffer, 0, Capacity);
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
                }
            }
            if (BufferValidTo + count < Capacity)
            {
                Array.Copy(buffer, offset, Buffer, BufferValidTo, count);
                BufferValidTo = (BufferValidTo + count) % Capacity;
            }
            else
            {
                var feedLength1 = Capacity - BufferValidTo;
                var feedLength2 = count - feedLength1;
                Array.Copy(buffer, offset, Buffer, BufferValidTo, feedLength1);
                Array.Copy(buffer, offset + feedLength1, Buffer, 0, feedLength2);
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
                    Array.Copy(Buffer, BufferValidFrom, buffer, offset, count);
                else
                {
                    var consumeCount1 = Math.Min(count, Capacity - BufferValidFrom);
                    var consumeCount2 = count - consumeCount1;
                    Array.Copy(Buffer, BufferValidFrom, buffer, offset, consumeCount1);
                    Array.Copy(Buffer, 0, buffer, offset + consumeCount1, consumeCount2);
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
                return Buffer[(BufferValidFrom + i) % Capacity];
            }
            set
            {
                if (i < 0 || i >= Length)
                    throw new ArgumentOutOfRangeException(nameof(i));
                Buffer[(BufferValidFrom + i) % Capacity] = value;
            }
        }

        public int Capacity => Buffer.Length;

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
                    Array.Clear(Buffer, BufferValidTo, newValidTo - BufferValidTo);
                else
                {
                    Array.Clear(Buffer, BufferValidTo, Capacity - BufferValidTo);
                    Array.Clear(Buffer, 0, newValidTo);
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
                Array.Copy(Buffer, readOffset, buffer, offset, count);
            else
            {
                var readCount1 = Capacity - readOffset;
                var readCount2 = count - readCount1;
                Array.Copy(Buffer, readOffset, buffer, offset, readCount1);
                Array.Copy(Buffer, 0, buffer, offset + readCount1, readCount2);
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
                Array.Copy(buffer, offset, Buffer, writeOffset, count);
            else
            {
                var writeCount1 = Capacity - writeOffset;
                var writeCount2 = count - writeCount1;
                Array.Copy(buffer, offset, Buffer, writeOffset, writeCount1);
                Array.Copy(buffer, offset + writeCount1, Buffer, 0, writeCount2);
            }
            ExternalPosition += count;
        }
    }
}
