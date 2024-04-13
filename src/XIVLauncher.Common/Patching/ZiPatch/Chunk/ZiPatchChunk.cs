using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    public abstract class ZiPatchChunk
    {
        public static string Type { get; protected set; }
        // Hack: C# doesn't let you get static fields from instances.
        public virtual string ChunkType => (string) GetType()
            .GetField("Type", BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public)
            ?.GetValue(null);

        public long Offset { get; protected set; }
        public long Size { get; protected set; }
        public uint Checksum { get; protected set; }

        /// <summary>
        /// The checksum calculated from the chunk's data.
        /// This WILL! be 0 if the file has not been loaded with checksums enabled.
        /// </summary>
        public uint CalculatedChecksum { get; protected set; }

        private bool _needsChecksum;
        protected readonly BinaryReader Reader;

        private static readonly AsyncLocal<MemoryStream> localMemoryStream = new AsyncLocal<MemoryStream>();

        // Only FileHeader, ApplyOption, Sqpk, and EOF have been observed in XIVARR+ patches
        // AddDirectory and DeleteDirectory can theoretically happen, so they're implemented
        // ApplyFreeSpace doesn't seem to show up anymore, and EntryFile will just error out
        private static readonly Dictionary<string, Func<BinaryReader, long, long, ZiPatchChunk>> ChunkTypes =
            new()
            {
                { FileHeaderChunk.Type, (reader, offset, size) => new FileHeaderChunk(reader, offset, size) },
                { ApplyOptionChunk.Type, (reader, offset, size) => new ApplyOptionChunk(reader, offset, size) },
                { ApplyFreeSpaceChunk.Type, (reader, offset, size) => new ApplyFreeSpaceChunk(reader, offset, size) },
                { AddDirectoryChunk.Type, (reader, offset, size) => new AddDirectoryChunk(reader, offset, size) },
                { DeleteDirectoryChunk.Type, (reader, offset, size) => new DeleteDirectoryChunk(reader, offset, size) },
                { SqpkChunk.Type, SqpkChunk.GetCommand },
                { EndOfFileChunk.Type, (reader, offset, size) => new EndOfFileChunk(reader, offset, size) },
                { XXXXChunk.Type, (reader, offset, size) => new XXXXChunk(reader, offset, size) }
            };


        public static ZiPatchChunk GetChunk(Stream stream, bool needsChecksum)
        {
            localMemoryStream.Value = localMemoryStream.Value ?? new MemoryStream();

            var memoryStream = localMemoryStream.Value;
            try
            {
                var reader = new BinaryReader(stream);
                var size = checked((int)reader.ReadUInt32BE());
                var baseOffset = stream.Position;

                // size of chunk + header + checksum
                var readSize = size + 4 + 4;

                // Enlarge MemoryStream if necessary, or set length at capacity
                var maxLen = Math.Max(readSize, memoryStream.Capacity);
                if (memoryStream.Length < maxLen)
                    memoryStream.SetLength(maxLen);

                // Read into MemoryStream's inner buffer
                reader.BaseStream.Read(memoryStream.GetBuffer(), 0, readSize);

                BinaryReader binaryReader;

                if (needsChecksum)
                {
                    var checksumBinaryReader = new ChecksumBinaryReader(memoryStream);
                    checksumBinaryReader.InitCrc32();
                    binaryReader = checksumBinaryReader;
                }
                else
                {
                    binaryReader = new BinaryReader(memoryStream);
                }

                var type = binaryReader.ReadFixedLengthString(4u);
                if (!ChunkTypes.TryGetValue(type, out var constructor))
                    throw new ZiPatchException();

                var chunk = constructor(binaryReader, baseOffset, size);

                chunk._needsChecksum = needsChecksum;
                chunk.ReadChunk();
                chunk.ReadChecksum();
                return chunk;
            }
            catch (EndOfStreamException e)
            {
                throw new ZiPatchException("Could not get chunk", e);
            }
            finally
            {
                memoryStream.Position = 0;
            }
        }

        protected ZiPatchChunk(BinaryReader reader, long offset, long size)
        {
            this.Reader = reader;

            Offset = offset;
            Size = size;
        }

        protected virtual void ReadChunk()
        {
            using var advanceAfter = this.GetAdvanceOnDispose();
        }

        public virtual void ApplyChunk(ZiPatchConfig config) {}

        protected void ReadChecksum()
        {
            if (_needsChecksum && this.Reader is ChecksumBinaryReader checksumBinaryReader) CalculatedChecksum = checksumBinaryReader.GetCrc32();
            Checksum = this.Reader.ReadUInt32BE();
        }

        protected AdvanceOnDispose GetAdvanceOnDispose()
        {
            return new AdvanceOnDispose(this.Reader, Size, _needsChecksum);
        }

        public bool IsChecksumValid => CalculatedChecksum == Checksum;
    }
}
