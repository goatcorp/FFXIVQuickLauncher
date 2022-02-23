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

            if (_streams.TryGetValue(path, out var stream))
                return stream;

            stream = SqexFileStream.WaitForStream(path, mode, tries, sleeptime);
            _streams.Add(path, stream);

            return stream;
        }

        public void Dispose()
        {
            foreach (var stream in _streams.Values)
                stream.Dispose();
        }
    }
}