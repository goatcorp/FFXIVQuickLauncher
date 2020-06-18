using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.Util;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch.Chunk.SqpkCommand
{
    class SqpkHeader : SqpkChunk
    {
        public new static string Command = "H";

        public enum TargetFileKind : byte
        {
            Dat = (byte)'D',
            Index = (byte)'I'
        }
        public enum TargetHeaderKind : byte
        {
            Version = (byte)'V',
            Index = (byte)'I',
            Data = (byte)'D'
        }

        private const int HEADER_SIZE = 1024;

        public TargetFileKind TargetFile { get; protected set; }
        public TargetHeaderKind TargetHeader { get; protected set; }
        public SqpackFile File { get; protected set; }

        public byte[] HeaderData { get; protected set; }

        public SqpkHeader(ChecksumBinaryReader reader, int size) : base(reader, size) {}

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            TargetFile = (TargetFileKind)reader.ReadByte();
            TargetHeader = (TargetHeaderKind)reader.ReadByte();
            reader.ReadByte(); // Alignment

            if (TargetFile == TargetFileKind.Dat)
                File = new SqpackDatFile(reader);
            else
                File = new SqpackIndexFile(reader);

            HeaderData = reader.ReadBytes(HEADER_SIZE);

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            File.ResolvePath(config.Platform);

            using (var file = File.OpenStream(config.GamePath, FileMode.OpenOrCreate))
                file.WriteFromOffset(HeaderData, TargetHeader == TargetHeaderKind.Version ? 0 : HEADER_SIZE);
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{TargetFile}:{TargetHeader}:{File}";
        }
    }
}
