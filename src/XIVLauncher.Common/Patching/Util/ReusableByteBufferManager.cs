using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private readonly int ArraySize;
        private readonly Allocation[] Buffers;

        public ReusableByteBufferManager(int arraySize, int maxBuffers)
        {
            ArraySize = arraySize;
            Buffers = new Allocation[maxBuffers];
        }

        public Allocation Allocate(bool clear = false)
        {
            Allocation res = null;

            for (int i = 0; i < Buffers.Length; i++)
            {
                if (Buffers[i] == null)
                    continue;

                lock (Buffers.SyncRoot)
                {
                    if (Buffers[i] == null)
                        continue;

                    res = Buffers[i];
                    Buffers[i] = null;
                    break;
                }
            }
            if (res == null)
                res = new Allocation(this, ArraySize);
            else if (clear)
                res.Clear();
            res.ResetState();
            return res;
        }

        internal void Return(Allocation buf)
        {
            for (int i = 0; i < Buffers.Length; i++)
            {
                if (Buffers[i] != null)
                    continue;

                lock (Buffers.SyncRoot)
                {
                    if (Buffers[i] != null)
                        continue;

                    Buffers[i] = buf;
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
