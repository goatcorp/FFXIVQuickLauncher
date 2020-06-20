using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;
using XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk
{
    public abstract class SqpkChunk : ZiPatchChunk
    {
        public new static string Type = "SQPK";
        public static string Command { get; protected set; }


        private static readonly Dictionary<string, Func<ChecksumBinaryReader, int, SqpkChunk>> CommandTypes =
            new Dictionary<string, Func<ChecksumBinaryReader, int, SqpkChunk>> {
                { SqpkAddData.Command, (reader, size) => new SqpkAddData(reader, size) },
                { SqpkDeleteData.Command, (reader, size) => new SqpkDeleteData(reader, size) },
                { SqpkHeader.Command, (reader, size) => new SqpkHeader(reader, size) },
                { SqpkTargetInfo.Command, (reader, size) => new SqpkTargetInfo(reader, size) },
                { SqpkExpandData.Command, (reader, size) => new SqpkExpandData(reader, size) },
                { SqpkIndex.Command, (reader, size) => new SqpkIndex(reader, size) },
                { SqpkFile.Command, (reader, size) => new SqpkFile(reader, size) },
                { SqpkPatchInfo.Command, (reader, size) => new SqpkPatchInfo(reader, size) }
            };

        public static ZiPatchChunk GetCommand(ChecksumBinaryReader reader, int size)
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

                var chunk = constructor(reader, innerSize - 5);

                return chunk;
            }
            catch (EndOfStreamException e)
            {
                throw new ZiPatchException();
            }
        }


        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        protected SqpkChunk(ChecksumBinaryReader reader, int size) : base(reader, size) {}

        public override string ToString()
        {
            return Type;
        }
    }
}
