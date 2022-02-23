using System;
using System.Collections.Generic;
using System.IO;

using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    /// <summary>
    /// An "SQPK" (SQEX Pack) chunk abstraction.
    /// </summary>
    public abstract class SqpkChunk : ZiPatchChunk
    {
        /// <summary>
        /// The chunk type.
        /// </summary>
        public static new string Type = "SQPK";

        /// <summary>
        /// Gets the chunk command.
        /// </summary>
        public static string Command { get; protected set; }

        private static readonly Dictionary<string, Func<ChecksumBinaryReader, int, int, SqpkChunk>> CommandTypes =
            new()
            {
                #pragma warning disable format // @formatter:off
                { SqpkAddData.Command,    (reader, offset, size) => new SqpkAddData(reader, offset, size) },
                { SqpkDeleteData.Command, (reader, offset, size) => new SqpkDeleteData(reader, offset, size) },
                { SqpkHeader.Command,     (reader, offset, size) => new SqpkHeader(reader, offset, size) },
                { SqpkTargetInfo.Command, (reader, offset, size) => new SqpkTargetInfo(reader, offset, size) },
                { SqpkExpandData.Command, (reader, offset, size) => new SqpkExpandData(reader, offset, size) },
                { SqpkIndex.Command,      (reader, offset, size) => new SqpkIndex(reader, offset, size) },
                { SqpkFile.Command,       (reader, offset, size) => new SqpkFile(reader, offset, size) },
                { SqpkPatchInfo.Command,  (reader, offset, size) => new SqpkPatchInfo(reader, offset, size) },
                #pragma warning restore format // @formatter:on
            };

        /// <summary>
        /// Gets the next command.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Command size.</param>
        /// <returns>A command chunk.</returns>
        public static ZiPatchChunk GetCommand(ChecksumBinaryReader reader, int offset, int size)
        {
            try
            {
                // Have not seen this differ from size
                var innerSize = reader.ReadInt32BE();
                if (size != innerSize)
                    throw new ZiPatchException();

                var command = reader.ReadFixedLengthString(1u);
                if (!CommandTypes.TryGetValue(command, out var constructor))
                    throw new ZiPatchException();

                var chunk = constructor(reader, offset, innerSize - 5);

                return chunk;
            }
            catch (EndOfStreamException e)
            {
                throw new ZiPatchException("Could not get command", e);
            }
        }

        /// <inheritdoc/>
        protected override void ReadChunk()
        {
            var start = this.Reader.BaseStream.Position;

            this.Reader.ReadBytes(Size - (int)(this.Reader.BaseStream.Position - start));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqpkChunk"/> class.
        /// </summary>
        /// <param name="reader">Binary reader.</param>
        /// <param name="offset">Chunk offset.</param>
        /// <param name="size">Chunk size.</param>
        protected SqpkChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size)
        { }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Type;
        }
    }
}
