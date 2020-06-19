using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using XIVLauncher.PatchInstaller.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk
{
    public abstract class ZiPatchChunk
    {
        public static string Type { get; protected set; }
        // Hack: C# doesn't let you get static fields from instances.
        public virtual string ChunkType => (string) GetType()
            .GetField("Type", BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public)
            ?.GetValue(null);

        public int Size { get; protected set; }
        public uint Checksum { get; protected set; }
        public uint CalculatedChecksum { get; protected set; }


        protected readonly ChecksumBinaryReader reader;

        private static AsyncLocal<MemoryStream> localMemoryStream = new AsyncLocal<MemoryStream> { Value = new MemoryStream() };


        // Only FileHeader, ApplyOption, Sqpk, and EOF have been observed in XIVARR+ patches
        // AddDirectory and DeleteDirectory can theoretically happen, so they're implemented
        // ApplyFreeSpace doesn't seem to show up anymore, and EntryFile will just error out
        private static readonly Dictionary<string, Func<ChecksumBinaryReader, int, ZiPatchChunk>> ChunkTypes =
            new Dictionary<string, Func<ChecksumBinaryReader, int, ZiPatchChunk>> {
                { FileHeaderChunk.Type, (reader, size) => new FileHeaderChunk(reader, size) },
                { ApplyOptionChunk.Type, (reader, size) => new ApplyOptionChunk(reader, size) },
                { ApplyFreeSpaceChunk.Type, (reader, size) => new ApplyFreeSpaceChunk(reader, size) },
                { AddDirectoryChunk.Type, (reader, size) => new AddDirectoryChunk(reader, size) },
                { DeleteDirectoryChunk.Type, (reader, size) => new DeleteDirectoryChunk(reader, size) },
                { SqpkChunk.Type, SqpkChunk.GetCommand },
                { EndOfFileChunk.Type, (reader, size) => new EndOfFileChunk(reader, size) },
                { XXXXChunk.Type, (reader, size) => new XXXXChunk(reader, size) }
        };


        public static ZiPatchChunk GetChunk(Stream stream)
        {
            var memoryStream = localMemoryStream.Value;
            try
            {
                var reader = new BinaryReader(stream);

                var size = reader.ReadInt32BE();

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


                var chunk = constructor(binaryReader, size);

                chunk.ReadChunk();
                chunk.ReadChecksum();
                return chunk;
            }
            catch (EndOfStreamException e)
            {
                throw new ZiPatchException();
            }
            finally
            {
                memoryStream.Position = 0;
            }
        }

        protected ZiPatchChunk(ChecksumBinaryReader reader, int size)
        {
            this.reader = reader;
            
            Size = size;
        }

        protected virtual void ReadChunk()
        {
            reader.ReadBytes(Size);
        }

        public virtual void ApplyChunk(ZiPatchConfig config) {}

        protected void ReadChecksum()
        {
            CalculatedChecksum = reader.GetCrc32();
            Checksum = reader.ReadUInt32BE();
        }

        public bool IsChecksumValid => CalculatedChecksum == Checksum;
    }
}
