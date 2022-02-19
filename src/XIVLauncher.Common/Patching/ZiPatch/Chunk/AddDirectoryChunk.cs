using System.IO;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    public class AddDirectoryChunk : ZiPatchChunk
    {
        public new static string Type = "ADIR";

        public string DirName { get; protected set; }

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            var dirNameLen = reader.ReadUInt32BE();

            DirName = reader.ReadFixedLengthString(dirNameLen);

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }


        public AddDirectoryChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) {}

        public override void ApplyChunk(ZiPatchConfig config)
        {
            Directory.CreateDirectory(config.GamePath + DirName);
        }

        public override string ToString()
        {
            return $"{Type}:{DirName}";
        }
    }
}