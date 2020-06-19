using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using XIVLauncher.PatchInstaller.Util;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand
{
    class SqpkAddData : SqpkChunk
    {
        public new static string Command = "A";


        public SqpackDatFile File { get; protected set; }
        public int BlockOffset { get; protected set; }
        public int BlockNumber { get; protected set; }
        public int BlockDeleteNumber { get; protected set; }

        public byte[] BlockData { get; protected set; }


        public SqpkAddData(ChecksumBinaryReader reader, int size) : base(reader, size) {}

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            reader.ReadBytes(3); // Alignment

            File = new SqpackDatFile(reader);

            BlockOffset = reader.ReadInt32BE() << 7;
            BlockNumber = reader.ReadInt32BE() << 7;
            BlockDeleteNumber = reader.ReadInt32BE() << 7;

            BlockData = reader.ReadBytes((int)BlockNumber);

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            File.ResolvePath(config.Platform);

            var file = File.OpenStream(config.GamePath, FileMode.OpenOrCreate);

            file.WriteFromOffset(BlockData, BlockOffset);
                
            file.Wipe(BlockDeleteNumber);
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{File}:{BlockOffset}:{BlockNumber}:{BlockDeleteNumber}";
        }
    }
}
