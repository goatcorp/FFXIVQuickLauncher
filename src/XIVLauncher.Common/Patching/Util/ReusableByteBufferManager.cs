using System;
using System.IO;
using System.Linq;

namespace XIVLauncher.Common.Patching.Util
{
    public class ReusableByteBufferManager
    {
        private static readonly int[] ArraySizes = new int[] { 1 << 14, 1 << 16, 1 << 18, 1 << 20, 1 << 22 };
        private static readonly ReusableByteBufferManager[] Instances = ArraySizes.Select(x => new ReusableByteBufferManager(x, 2 * Environment.ProcessorCount)).ToArray();

        public class Allocation : IDisposable
        {
            public readonly ReusableByteBufferManager BufferManager;
            public readonly byte[] Buffer;
            public readonly MemoryStream Stream;
            public readonly BinaryWriter Writer;

            internal Allocation(ReusableByteBufferManager b, long size)
            {
                BufferManager = b;
                Buffer = new byte[size];
                Stream = new MemoryStream(Buffer);
                Writer = new BinaryWriter(Stream);
            }

            public void ResetState()
            {
                Stream.SetLength(0);
                Stream.Seek(0, SeekOrigin.Begin);
            }

            public void Clear() => Array.Clear(Buffer, 0, Buffer.Length);

            public void Dispose() => BufferManager?.Return(this);
        }

        private readonly int arraySize;
        private readonly Allocation[] buffers;

        public ReusableByteBufferManager(int arraySize, int maxBuffers)
        {
            this.arraySize = arraySize;
            this.buffers = new Allocation[maxBuffers];
        }

        public Allocation Allocate(bool clear = false)
        {
            Allocation res = null;

            for (int i = 0; i < this.buffers.Length; i++)
            {
                if (this.buffers[i] == null)
                    continue;

                lock (this.buffers.SyncRoot)
                {
                    if (this.buffers[i] == null)
                        continue;

                    res = this.buffers[i];
                    this.buffers[i] = null;
                    break;
                }
            }
            if (res == null)
                res = new Allocation(this, this.arraySize);
            else if (clear)
                res.Clear();
            res.ResetState();
            return res;
        }

        internal void Return(Allocation buf)
        {
            for (int i = 0; i < this.buffers.Length; i++)
            {
                if (this.buffers[i] != null)
                    continue;

                lock (this.buffers.SyncRoot)
                {
                    if (this.buffers[i] != null)
                        continue;

                    this.buffers[i] = buf;
                    return;
                }
            }
        }

        public static Allocation GetBuffer(bool clear = false)
        {
            return Instances[0].Allocate(clear);
        }

        public static Allocation GetBuffer(long minSize, bool clear = false)
        {
            for (int i = 0; i < ArraySizes.Length; i++)
                if (ArraySizes[i] >= minSize)
                    return Instances[i].Allocate(clear);

            return new Allocation(null, minSize);
        }
    }
}
