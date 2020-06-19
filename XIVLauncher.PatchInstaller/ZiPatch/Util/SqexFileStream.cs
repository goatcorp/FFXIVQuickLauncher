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
        private class StreamStore : IDisposable
        {
            public string Path { get; set; }
            public SqexFileStream Stream { get; set; }

            public void Dispose() => Stream?.Dispose();
        }

        private static byte[] WIPE_BUFFER = new byte[1 << 16];

        private static AsyncLocal<StreamStore> streams =
            new AsyncLocal<StreamStore> {Value = new StreamStore()};

        public SqexFileStream(string path, FileMode mode) : base(path, mode, FileAccess.ReadWrite, FileShare.Read, 1 << 16)
        {}

        public static SqexFileStream WaitForStream(string path, FileMode mode, int tries = 5, int sleeptime = 1, bool store = true)
        {
            if (streams.Value.Path == path)
                return streams.Value.Stream;
            
            streams.Value.Stream?.Dispose();
            streams.Value.Path = null;

            do
            {
                try
                {
                    var stream = new SqexFileStream(path, mode);

                    if (!store) return stream;
                    
                    streams.Value.Path = path;
                    streams.Value.Stream = stream;

                    return stream;
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
