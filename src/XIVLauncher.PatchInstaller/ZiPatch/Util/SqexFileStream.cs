using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.ZiPatch.Util
{
    /// <summary>
    /// An SQEX filestream.
    /// </summary>
    public class SqexFileStream : FileStream
    {
        private static readonly byte[] WipeBuffer = new byte[1 << 16];

        public SqexFileStream(string path, FileMode mode) : base(path, mode, FileAccess.ReadWrite, FileShare.Read, 1 << 16)
        {}

        /// <summary>
        /// Wait for a stream to be available.
        /// </summary>
        /// <param name="path">File path.</param>
        /// <param name="mode">Read/write mode.</param>
        /// <param name="tries">Attempts.</param>
        /// <param name="sleeptime">Interval between attempts.</param>
        /// <returns>A filestream.</returns>
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

        /// <summary>
        /// Write data at a given offset.
        /// </summary>
        /// <param name="data">Data to write.</param>
        /// <param name="offset">Starting offset.</param>
        public void WriteFromOffset(byte[] data, int offset)
        {
            Seek(offset, SeekOrigin.Begin);
            Write(data, 0, data.Length);
        }

        /// <summary>
        /// Wipe an amount of data.
        /// </summary>
        /// <param name="length">Amount to wipe.</param>
        public void Wipe(int length)
        {
            for (int numBytes; length > 0; length -= numBytes)
            {
                numBytes = Math.Min(WipeBuffer.Length, length);
                Write(WipeBuffer, 0, numBytes);
            }
        }

        /// <summary>
        /// Wipe an amount of data at a given offset.
        /// </summary>
        /// <param name="length">Amount to wipe.</param>
        /// <param name="offset">Starting offset.</param>
        public void WipeFromOffset(int length, int offset)
        {
            Seek(offset, SeekOrigin.Begin);
            Wipe(length);
        }
    }
}
