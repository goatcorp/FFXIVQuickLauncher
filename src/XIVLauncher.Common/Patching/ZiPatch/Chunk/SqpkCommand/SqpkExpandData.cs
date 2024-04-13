using System.IO;
using XIVLauncher.Common.Patching.Util;
using XIVLauncher.Common.Patching.ZiPatch.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    class SqpkExpandData : SqpkChunk
    {
        public new static string Command = "E";


        public SqpackDatFile TargetFile { get; protected set; }
        public long BlockOffset { get; protected set; }
        public long BlockNumber { get; protected set; }


        public SqpkExpandData(BinaryReader reader, long offset, long size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            using var advanceAfter = this.GetAdvanceOnDispose();
            this.Reader.ReadBytes(3);

            TargetFile = new SqpackDatFile(this.Reader);

            BlockOffset = (long)this.Reader.ReadUInt32BE() << 7;
            BlockNumber = (long)this.Reader.ReadUInt32BE();

            this.Reader.ReadUInt32(); // Reserved
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            TargetFile.ResolvePath(config.Platform);

            var file = config.Store == null ?
                TargetFile.OpenStream(config.GamePath, FileMode.OpenOrCreate) :
                TargetFile.OpenStream(config.Store, config.GamePath, FileMode.OpenOrCreate);

            SqpackDatFile.WriteEmptyFileBlockAt(file, BlockOffset, BlockNumber);
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{BlockOffset}:{BlockNumber}";
        }
    }
}
