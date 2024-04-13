using System.IO;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
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

        public const int HEADER_SIZE = 1024;

        public TargetFileKind FileKind { get; protected set; }
        public TargetHeaderKind HeaderKind { get; protected set; }
        public SqpackFile TargetFile { get; protected set; }

        public byte[] HeaderData { get; protected set; }
        public long HeaderDataSourceOffset { get; protected set; }

        public SqpkHeader(BinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            using var advanceAfter = this.GetAdvanceOnDispose();
            FileKind = (TargetFileKind)this.Reader.ReadByte();
            HeaderKind = (TargetHeaderKind)this.Reader.ReadByte();
            this.Reader.ReadByte(); // Alignment

            if (FileKind == TargetFileKind.Dat)
                TargetFile = new SqpackDatFile(this.Reader);
            else
                TargetFile = new SqpackIndexFile(this.Reader);

            HeaderDataSourceOffset = Offset + this.Reader.BaseStream.Position;
            HeaderData = this.Reader.ReadBytes(HEADER_SIZE);
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            TargetFile.ResolvePath(config.Platform);

            var file = config.Store == null ?
                TargetFile.OpenStream(config.GamePath, FileMode.OpenOrCreate) :
                TargetFile.OpenStream(config.Store, config.GamePath, FileMode.OpenOrCreate);

            file.WriteFromOffset(HeaderData, HeaderKind == TargetHeaderKind.Version ? 0 : HEADER_SIZE);
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{FileKind}:{HeaderKind}:{TargetFile}";
        }
    }
}
