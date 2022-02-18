using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.Util
{
    public class ReusableByteBufferManager
    {
        private const int DEFAULT_EXPONENTIAL_BUFFER_SIZE = 12;  // 4096
        private static readonly ReusableByteBufferManager[] Instances = new ReusableByteBufferManager[32];

        public class Allocation : IDisposable
        {
            public readonly ReusableByteBufferManager BufferManager;
            public readonly byte[] Buffer;
            public readonly MemoryStream Stream;
            public readonly BinaryWriter Writer;

            internal Allocation(ReusableByteBufferManager b)
            {
                BufferManager = b;
                Buffer = new byte[b.ArraySize];
                Stream = new MemoryStream(Buffer);
                Writer = new BinaryWriter(Stream);
            }

            public void ResetState()
            {
                Stream.SetLength(0);
                Stream.Seek(0, SeekOrigin.Begin);
            }

            public void Clear() => Array.Clear(Buffer, 0, Buffer.Length);

            public void Dispose() => BufferManager.Return(this);
        }

        private readonly int ArraySize;
        private readonly Allocation[] Buffers;

        public ReusableByteBufferManager(int arraySize, int maxBuffers = 0)
        {
            if (maxBuffers == 0)
                maxBuffers = Environment.ProcessorCount;

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

                lock (Buffers)
                {
                    if (Buffers[i] == null)
                        continue;

                    res = Buffers[i];
                    Buffers[i] = null;
                    break;
                }
            }
            if (res == null)
                res = new Allocation(this);
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

                lock (Buffers)
                {
                    if (Buffers[i] != null)
                        continue;

                    Buffers[i] = buf;
                    return;
                }
            }
        }

        public static ReusableByteBufferManager GetInstance(int exponentialArraySize = -1)
        {
            if (exponentialArraySize == -1)
                exponentialArraySize = DEFAULT_EXPONENTIAL_BUFFER_SIZE;
            if (Instances[exponentialArraySize] == null)
            {
                lock (Instances)
                {
                    if (Instances[exponentialArraySize] == null)
                    {
                        Instances[exponentialArraySize] = new ReusableByteBufferManager(1 << exponentialArraySize);
                    }
                }
            }
            return Instances[exponentialArraySize];
        }

        public static Allocation GetBuffer(int exponentialArraySize = -1, bool clear = false)
        {
            return GetInstance(exponentialArraySize).Allocate(clear);
        }
    }
}
