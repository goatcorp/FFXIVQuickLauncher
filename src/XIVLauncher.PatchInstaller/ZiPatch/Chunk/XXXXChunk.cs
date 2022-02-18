using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk
{
    public class XXXXChunk : ZiPatchChunk
    {
        // TODO: This... Never happens.
        public new static string Type = "XXXX";


        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }


        public XXXXChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) {}


        public override string ToString()
        {
            return Type;
        }
    }
}
