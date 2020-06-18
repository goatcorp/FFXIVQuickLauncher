using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.ZiPatch.Util
{
    public class SqexFileStream : FileStream
    {
        private static byte[] WIPE_BUFFER = new byte[1 << 16];

        public SqexFileStream(string path, FileMode mode) : base(path, mode)
        {
        }

        public static SqexFileStream WaitForStream(string path, FileMode mode, int tries = 5, int sleeptime = 1)
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
            } while (0 <-- tries);

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
                numBytes = Math.Min(WIPE_BUFFER.Length, length);
                Write(WIPE_BUFFER, 0, numBytes);
            }
        }

        public void WipeFromOffset(int length, int offset)
        {
            Seek(offset, SeekOrigin.Begin);
            Wipe(length);
        }
    }
}
