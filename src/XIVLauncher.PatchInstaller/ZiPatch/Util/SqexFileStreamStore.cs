using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.ZiPatch.Util
{
    /// <summary>
    /// An SQEX filestream store.
    /// </summary>
    public class SqexFileStreamStore : IDisposable
    {
        private readonly Dictionary<string, SqexFileStream> _streams = new Dictionary<string, SqexFileStream>();

        /// <summary>
        /// Get a stream by its path.
        /// </summary>
        /// <param name="path">Filepath.</param>
        /// <param name="mode">Read/write mode.</param>
        /// <param name="tries">Attempts.</param>
        /// <param name="sleeptime">Interval between attempts.</param>
        /// <returns>A filestream.</returns>
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

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var stream in _streams.Values)
                stream.Dispose();
        }
    }
}
