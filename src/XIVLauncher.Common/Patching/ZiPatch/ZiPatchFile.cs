using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XIVLauncher.Common.Patching.ZiPatch.Util;
using XIVLauncher.Common.Patching.ZiPatch.Chunk;

namespace XIVLauncher.Common.Patching.ZiPatch
{
    public class ZiPatchFile : IDisposable
    {
        private static readonly uint[] zipatchMagic =
        {
            0x50495A91, 0x48435441, 0x0A1A0A0D
        };

        private readonly Stream _stream;
        private readonly bool _needsChecksum;

        /// <summary>
        /// Instantiates a ZiPatchFile from a Stream
        /// </summary>
        /// <param name="stream">Stream to a ZiPatch</param>
        public ZiPatchFile(Stream stream, bool needsChecksum = false)
        {
            _stream = stream;
            _needsChecksum = needsChecksum;

            var reader = new BinaryReader(stream);

            if (zipatchMagic.Any(magic => magic != reader.ReadUInt32()))
            {
                stream.Dispose();
                throw new ZiPatchException();
            }
        }

        /// <summary>
        /// Instantiates a ZiPatchFile from a file path
        /// </summary>
        /// <param name="filepath">Path to patch file</param>
        public static ZiPatchFile FromFileName(string filepath)
        {
            var stream = SqexFileStream.WaitForStream(filepath, FileMode.Open);
            return new ZiPatchFile(stream);
        }

        public IEnumerable<ZiPatchChunk> GetChunks()
        {
            ZiPatchChunk chunk;

            do
            {
                chunk = ZiPatchChunk.GetChunk(_stream, _needsChecksum);

                yield return chunk;
            }
            while (chunk.ChunkType != EndOfFileChunk.Type);
        }

        public void Dispose()
        {
            _stream?.Dispose();
        }
    }
}
