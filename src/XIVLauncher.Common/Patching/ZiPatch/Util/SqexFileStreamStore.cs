using System;
using System.Collections.Generic;
using System.IO;

namespace XIVLauncher.Common.Patching.ZiPatch.Util
{
    public class SqexFileStreamStore : IDisposable
    {
        private readonly Dictionary<string, SqexFileStream> _streams = new Dictionary<string, SqexFileStream>();

        public SqexFileStream GetStream(string path, FileMode mode, int tries, int sleeptime)
        {
            // Normalise path
            path = Path.GetFullPath(path);
            var key = path.ToLowerInvariant();

            if (_streams.TryGetValue(key, out var stream))
                return stream;

            stream = SqexFileStream.WaitForStream(path, mode, tries, sleeptime);
            _streams.Add(key, stream);

            return stream;
        }

        public void CloseStream(string path)
        {
            path = Path.GetFullPath(path);
            var key = path.ToLowerInvariant();

            if (this._streams.TryGetValue(key, out var s))
            {
                this._streams.Remove(key);
                s.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var stream in _streams.Values)
            {
                stream.Flush(true);
                stream.Dispose();
            }

            this._streams.Clear();
        }
    }
}
