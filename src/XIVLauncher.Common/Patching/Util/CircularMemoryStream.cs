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
        private int _Length = 0;
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

            var newBuffer = ReusableByteBufferManager.GetBuffer(capacity);
            if (_Length > 0)
            {
                if (BufferValidFrom < BufferValidTo)
                    Array.Copy(ReusableBuffer.Buffer, BufferValidFrom, newBuffer.Buffer, 0, _Length);
                else
                {
                    Array.Copy(ReusableBuffer.Buffer, BufferValidFrom, newBuffer.Buffer, 0, Capacity - BufferValidFrom);
                    Array.Copy(ReusableBuffer.Buffer, 0, newBuffer.Buffer, Capacity - BufferValidFrom, BufferValidTo);
                }
            }

            ReusableBuffer.Dispose();
            ReusableBuffer = newBuffer;

            BufferValidFrom = 0;
            BufferValidTo = _Length;
        }

        public void Feed(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;

            if (_Length + count > Capacity)
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
                            _Length = Capacity;
                            return;
                        }
                        Consume(null, 0, _Length + count - Capacity);
                        break;

                    case FeedOverflowMode.Throw:
                        throw new InvalidOperationException($"Cannot feed {count} bytes (length={Length}, capacity={Capacity})");
                }
            }

            if (BufferValidFrom < BufferValidTo)
            {
                var rightLength = Capacity - BufferValidTo;
                if (rightLength >= count)
                    Buffer.BlockCopy(buffer, offset, ReusableBuffer.Buffer, BufferValidTo, count);
                else
                {
                    Buffer.BlockCopy(buffer, offset, ReusableBuffer.Buffer, BufferValidTo, rightLength);
                    Buffer.BlockCopy(buffer, offset + rightLength, ReusableBuffer.Buffer, 0, count - rightLength);
                }
            }
            else
                Buffer.BlockCopy(buffer, offset, ReusableBuffer.Buffer, BufferValidTo, count);

            BufferValidTo = (BufferValidTo + count) % Capacity;
            _Length += count;
        }

        public int Consume(byte[] buffer, int offset, int count, bool peek = false)
        {
            count = Math.Min(count, _Length);
            if (buffer != null && count > 0)
            {
                if (BufferValidFrom < BufferValidTo)
                    Buffer.BlockCopy(ReusableBuffer.Buffer, BufferValidFrom, buffer, offset, count);
                else
                {
                    int rightLength = Capacity - BufferValidFrom;
                    if (rightLength >= count)
                        Buffer.BlockCopy(ReusableBuffer.Buffer, BufferValidFrom, buffer, offset, count);
                    else
                    {
                        Buffer.BlockCopy(ReusableBuffer.Buffer, BufferValidFrom, buffer, offset, rightLength);
                        Buffer.BlockCopy(ReusableBuffer.Buffer, 0, buffer, offset + rightLength, count - rightLength);
                    }
                }
            }
            if (!peek)
            {
                _Length -= count;
                if (_Length == 0)
                    BufferValidFrom = BufferValidTo = 0;
                else
                    BufferValidFrom = (BufferValidFrom + count) % Capacity;
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
        public override long Length => _Length;
        public override long Position
        {
            get => ExternalPosition;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() { }

        public override void SetLength(long value)
        {
            if (value > int.MaxValue)
                throw new ArgumentOutOfRangeException("Length can be up to int.MaxValue");
            if (value == 0)
            {
                BufferValidFrom = BufferValidTo = _Length = 0;
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
            _Length = (int)value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = Math.Min(count, _Length - ExternalPosition);

            var adjValidFrom = (BufferValidFrom + ExternalPosition) % Capacity;
            if (adjValidFrom < BufferValidTo)
                Buffer.BlockCopy(ReusableBuffer.Buffer, adjValidFrom, buffer, offset, count);
            else
            {
                int rightLength = Capacity - adjValidFrom;
                if (rightLength >= count)
                    Buffer.BlockCopy(ReusableBuffer.Buffer, adjValidFrom, buffer, offset, count);
                else
                {
                    Buffer.BlockCopy(ReusableBuffer.Buffer, adjValidFrom, buffer, offset, rightLength);
                    Buffer.BlockCopy(ReusableBuffer.Buffer, 0, buffer, offset + rightLength, count - rightLength);
                }
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
            if (newPosition > _Length)
                newPosition = _Length;
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
