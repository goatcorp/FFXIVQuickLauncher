using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    public class EndOfFileChunk : ZiPatchChunk
    {
        public new static string Type = "EOF_";

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }


        public EndOfFileChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) {}

        public override string ToString()
        {
            return Type;
        }
    }
}