using System;
using System.IO;

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

        private readonly FeedOverflowMode overflowMode;
        private ReusableByteBufferManager.Allocation reusableBuffer;
        private int bufferValidTo = 0;
        private int bufferValidFrom = 0;
        private int length = 0;
        private int externalPosition = 0;

        public CircularMemoryStream(int baseCapacity = 0, FeedOverflowMode feedOverflowMode = FeedOverflowMode.ExtendCapacity)
        {
            this.overflowMode = feedOverflowMode;
            if (feedOverflowMode == FeedOverflowMode.ExtendCapacity && baseCapacity == 0)
                this.reusableBuffer = ReusableByteBufferManager.GetBuffer();
            else
                this.reusableBuffer = ReusableByteBufferManager.GetBuffer(baseCapacity);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this.reusableBuffer?.Dispose();
        }

        public void Reserve(long capacity)
        {
            if (capacity <= Capacity)
                return;

            var newBuffer = ReusableByteBufferManager.GetBuffer(capacity);
            if (this.length > 0)
            {
                if (this.bufferValidFrom < this.bufferValidTo)
                    Array.Copy(this.reusableBuffer.Buffer, this.bufferValidFrom, newBuffer.Buffer, 0, this.length);
                else
                {
                    Array.Copy(this.reusableBuffer.Buffer, this.bufferValidFrom, newBuffer.Buffer, 0, Capacity - this.bufferValidFrom);
                    Array.Copy(this.reusableBuffer.Buffer, 0, newBuffer.Buffer, Capacity - this.bufferValidFrom, this.bufferValidTo);
                }
            }

            this.reusableBuffer.Dispose();
            this.reusableBuffer = newBuffer;

            this.bufferValidFrom = 0;
            this.bufferValidTo = this.length;
        }

        public void Feed(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;

            if (this.length + count > Capacity)
            {
                switch (this.overflowMode)
                {
                    case FeedOverflowMode.ExtendCapacity:
                        Reserve(Length + count);
                        break;

                    case FeedOverflowMode.DiscardOldest:
                        if (count >= Capacity)
                        {
                            this.bufferValidFrom = 0;
                            this.bufferValidTo = 0;
                            Array.Copy(buffer, offset + count - Capacity, this.reusableBuffer.Buffer, 0, Capacity);
                            this.externalPosition = 0;
                            this.length = Capacity;
                            return;
                        }
                        Consume(null, 0, this.length + count - Capacity);
                        break;

                    case FeedOverflowMode.Throw:
                        throw new InvalidOperationException($"Cannot feed {count} bytes (length={Length}, capacity={Capacity})");
                }
            }

            if (this.bufferValidFrom < this.bufferValidTo)
            {
                var rightLength = Capacity - this.bufferValidTo;
                if (rightLength >= count)
                    Buffer.BlockCopy(buffer, offset, this.reusableBuffer.Buffer, this.bufferValidTo, count);
                else
                {
                    Buffer.BlockCopy(buffer, offset, this.reusableBuffer.Buffer, this.bufferValidTo, rightLength);
                    Buffer.BlockCopy(buffer, offset + rightLength, this.reusableBuffer.Buffer, 0, count - rightLength);
                }
            }
            else
                Buffer.BlockCopy(buffer, offset, this.reusableBuffer.Buffer, this.bufferValidTo, count);

            this.bufferValidTo = (this.bufferValidTo + count) % Capacity;
            this.length += count;
        }

        public int Consume(byte[] buffer, int offset, int count, bool peek = false)
        {
            count = Math.Min(count, this.length);
            if (buffer != null && count > 0)
            {
                if (this.bufferValidFrom < this.bufferValidTo)
                    Buffer.BlockCopy(this.reusableBuffer.Buffer, this.bufferValidFrom, buffer, offset, count);
                else
                {
                    int rightLength = Capacity - this.bufferValidFrom;
                    if (rightLength >= count)
                        Buffer.BlockCopy(this.reusableBuffer.Buffer, this.bufferValidFrom, buffer, offset, count);
                    else
                    {
                        Buffer.BlockCopy(this.reusableBuffer.Buffer, this.bufferValidFrom, buffer, offset, rightLength);
                        Buffer.BlockCopy(this.reusableBuffer.Buffer, 0, buffer, offset + rightLength, count - rightLength);
                    }
                }
            }
            if (!peek)
            {
                this.length -= count;
                if (this.length == 0)
                    this.bufferValidFrom = this.bufferValidTo = 0;
                else
                    this.bufferValidFrom = (this.bufferValidFrom + count) % Capacity;
                this.externalPosition = Math.Max(0, this.externalPosition - count);
            }
            return count;
        }

        public byte this[long i]
        {
            get
            {
                if (i < 0 || i >= Length)
                    throw new ArgumentOutOfRangeException(nameof(i));
                return this.reusableBuffer.Buffer[(this.bufferValidFrom + i) % Capacity];
            }
            set
            {
                if (i < 0 || i >= Length)
                    throw new ArgumentOutOfRangeException(nameof(i));
                this.reusableBuffer.Buffer[(this.bufferValidFrom + i) % Capacity] = value;
            }
        }

        public int Capacity => this.reusableBuffer.Buffer.Length;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => this.length;
        public override long Position
        {
            get => this.externalPosition;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush() { }

        public override void SetLength(long value)
        {
            if (value > int.MaxValue)
                throw new ArgumentOutOfRangeException("Length can be up to int.MaxValue");
            if (value == 0)
            {
                this.bufferValidFrom = this.bufferValidTo = this.length = 0;
                return;
            }

            var intValue = (int)value;
            if (intValue > Capacity)
                Reserve(intValue);
            else if (intValue > Length)
            {
                var extendLength = (int)(intValue - Length);
                var newValidTo = (this.bufferValidTo + extendLength) % Capacity;

                if (this.bufferValidTo < newValidTo)
                    Array.Clear(this.reusableBuffer.Buffer, this.bufferValidTo, newValidTo - this.bufferValidTo);
                else
                {
                    Array.Clear(this.reusableBuffer.Buffer, this.bufferValidTo, Capacity - this.bufferValidTo);
                    Array.Clear(this.reusableBuffer.Buffer, 0, newValidTo);
                }

                this.bufferValidTo = newValidTo;
            }
            else if (intValue < Length)
                this.bufferValidTo = (this.bufferValidFrom + intValue) % Capacity;
            this.length = (int)value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = Math.Min(count, this.length - this.externalPosition);

            var adjValidFrom = (this.bufferValidFrom + this.externalPosition) % Capacity;
            if (adjValidFrom < this.bufferValidTo)
                Buffer.BlockCopy(this.reusableBuffer.Buffer, adjValidFrom, buffer, offset, count);
            else
            {
                int rightLength = Capacity - adjValidFrom;
                if (rightLength >= count)
                    Buffer.BlockCopy(this.reusableBuffer.Buffer, adjValidFrom, buffer, offset, count);
                else
                {
                    Buffer.BlockCopy(this.reusableBuffer.Buffer, adjValidFrom, buffer, offset, rightLength);
                    Buffer.BlockCopy(this.reusableBuffer.Buffer, 0, buffer, offset + rightLength, count - rightLength);
                }
            }

            this.externalPosition += count;
            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = this.externalPosition;
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
            if (newPosition > this.length)
                newPosition = this.length;
            this.externalPosition = (int)newPosition;
            return newPosition;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Length + count > Capacity)
                Reserve((int)(Length + count));

            var writeOffset = (this.bufferValidFrom + this.externalPosition) % Capacity;
            if (writeOffset + count <= Capacity)
                Array.Copy(buffer, offset, this.reusableBuffer.Buffer, writeOffset, count);
            else
            {
                var writeCount1 = Capacity - writeOffset;
                var writeCount2 = count - writeCount1;
                Array.Copy(buffer, offset, this.reusableBuffer.Buffer, writeOffset, writeCount1);
                Array.Copy(buffer, offset + writeCount1, this.reusableBuffer.Buffer, 0, writeCount2);
            }
            this.externalPosition += count;
        }
    }
}
