using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    /// <summary>
    /// Base chunk abstraction.
    /// </summary>
    public abstract class ZiPatchChunk
    {
        /// <summary>
        /// The chunk type.
        /// </summary>
        public static string Type { get; protected set; }

        // Hack: C# doesn't let you get static fields from instances.
        public virtual string ChunkType => (string)GetType()
            .GetField("Type", BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public)
            ?.GetValue(null);

        /// <summary>
        /// Gets the chunk offset.
        /// </summary>
        public int Offset { get; protected set; }

        /// <summary>
        /// Gets the chunk size.
        /// </summary>
        public int Size { get; protected set; }

        /// <summary>
        /// Gets the CRC32 from the chunk.
        /// </summary>
        public uint Checksum { get; protected set; }

        /// <summary>
        /// Gets the calculated CRC32 from the chunk reader.
        /// </summary>
        public uint CalculatedChecksum { get; protected set; }

        /// <summary>
        /// Gets the binary reader.
        /// </summary>
        private protected ChecksumBinaryReader Reader { get; }

        private static readonly AsyncLocal<MemoryStream> localMemoryStream = new AsyncLocal<MemoryStream>();

        // Only FileHeader, ApplyOption, Sqpk, and EOF have been observed in XIVARR+ patches
        // AddDirectory and DeleteDirectory can theoretically happen, so they're implemented
        // ApplyFreeSpace doesn't seem to show up anymore, and EntryFile will just error out
        private static readonly Dictionary<string, Func<ChecksumBinaryReader, int, int, ZiPatchChunk>> ChunkTypes =
            new()
            {
                #pragma warning disable format // @formatter:off
                { FileHeaderChunk.Type,      (reader, offset, size) => new FileHeaderChunk(reader, offset, size) },
                { ApplyOptionChunk.Type,     (reader, offset, size) => new ApplyOptionChunk(reader, offset, size) },
                { ApplyFreeSpaceChunk.Type,  (reader, offset, size) => new ApplyFreeSpaceChunk(reader, offset, size) },
                { AddDirectoryChunk.Type,    (reader, offset, size) => new AddDirectoryChunk(reader, offset, size) },
                { DeleteDirectoryChunk.Type, (reader, offset, size) => new DeleteDirectoryChunk(reader, offset, size) },
                { EndOfFileChunk.Type,       (reader, offset, size) => new EndOfFileChunk(reader, offset, size) },
                { XXXXChunk.Type,            (reader, offset, size) => new XXXXChunk(reader, offset, size) },
                { SqpkChunk.Type,            SqpkChunk.GetCommand },
                #pragma warning restore format // @formatter:on
            };

        /// <summary>
        /// Get the next available chunk.
        /// </summary>
        /// <param name="stream">Memory stream to read from.</param>
        /// <returns>A chunk.</returns>
        public static ZiPatchChunk GetChunk(Stream stream)
        {
            localMemoryStream.Value = localMemoryStream.Value ?? new MemoryStream();

            var memoryStream = localMemoryStream.Value;
            try
            {
                var reader = new BinaryReader(stream);
                var size = reader.ReadInt32BE();
                var baseOffset = (int)stream.Position;

                // size of chunk + header + checksum
                var readSize = size + 4 + 4;

                // Enlarge MemoryStream if necessary, or set length at capacity
                var maxLen = Math.Max(readSize, memoryStream.Capacity);
                if (memoryStream.Length < maxLen)
                    memoryStream.SetLength(maxLen);

                // Read into MemoryStream's inner buffer
                reader.BaseStream.Read(memoryStream.GetBuffer(), 0, readSize);

                var binaryReader = new ChecksumBinaryReader(memoryStream);
                binaryReader.InitCrc32();

                var type = binaryReader.ReadFixedLengthString(4u);
                if (!ChunkTypes.TryGetValue(type, out var constructor))
                    throw new ZiPatchException();

                var chunk = constructor(binaryReader, baseOffset, size);

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

        /// <summary>
        /// Initializes a new instance of the <see cref="ZiPatchChunk"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Byte offset.</param>
        /// <param name="size">Chunk size.</param>
        protected ZiPatchChunk(ChecksumBinaryReader reader, int offset, int size)
        {
            this.Reader = reader;

            Offset = offset;
            Size = size;
        }

        /// <summary>
        /// Read a chunk.
        /// </summary>
        protected virtual void ReadChunk()
        {
            this.Reader.ReadBytes(Size);
        }

        /// <summary>
        /// Apply a chunk.
        /// </summary>
        /// <param name="config">Configuration.</param>
        public virtual void ApplyChunk(ZiPatchConfig config) { }

        /// <summary>
        /// Read the checksum and store it.
        /// </summary>
        protected void ReadChecksum()
        {
            CalculatedChecksum = this.Reader.GetCrc32();
            Checksum = this.Reader.ReadUInt32BE();
        }

        /// <summary>
        /// Gets a value indicating whether the checksums obtained from <see cref="ReadChecksum"/> are valid.
        /// </summary>
        public bool IsChecksumValid => CalculatedChecksum == Checksum;
    }
}
