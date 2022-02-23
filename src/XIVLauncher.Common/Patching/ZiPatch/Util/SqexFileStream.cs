using System;
using System.IO;
using System.Threading;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    public class SqexFileStream : FileStream
    {
        private static readonly byte[] WipeBuffer = new byte[1 << 16];

        public SqexFileStream(string path, FileMode mode) : base(path, mode, FileAccess.ReadWrite, FileShare.Read, 1 << 16)
        {}

        public static SqexFileStream? WaitForStream(string path, FileMode mode, int tries = 5, int sleeptime = 1)
        {
            do
            {
                try
                {
                    return new SqexFileStream(path, mode);
                }
                catch (IOException)
                {
                    if (tries == 0)
                        throw;

                    Thread.Sleep(sleeptime * 1000);
                }
            } while (0 < --tries);

            return null;
        }

        public void WriteFromOffset(byte[] data, int offset)
        {
            Seek(offset, SeekOrigin.Begin);
            Write(data, 0, data.Length);
        }

        public void Wipe(int length)
        {
            for (int numBytes; length > 0; length -= numBytes)
            {
                numBytes = Math.Min(WipeBuffer.Length, length);
                Write(WipeBuffer, 0, numBytes);
            }
        }

        public void WipeFromOffset(int length, int offset)
        {
            Seek(offset, SeekOrigin.Begin);
            Wipe(length);
        }
    }
}