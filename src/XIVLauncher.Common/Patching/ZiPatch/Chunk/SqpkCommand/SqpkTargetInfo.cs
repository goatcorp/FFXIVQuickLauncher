using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk.SqpkCommand
{
    class SqpkTargetInfo : SqpkChunk
    {
        // Only Platform is used on recent patcher versions
        public new static string Command = "T";


        // US/EU/JP are Global
        // ZH seems to also be Global
        // KR is unknown
        public enum RegionId : short
        {
            Global = -1
        }

        public ZiPatchConfig.PlatformId Platform { get; protected set; }
        public RegionId Region { get; protected set; }
        public bool IsDebug { get; protected set; }
        public ushort Version { get; protected set; }
        public ulong DeletedDataSize { get; protected set; }
        public ulong SeekCount { get; protected set; }



        public SqpkTargetInfo(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) {}


        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            // Reserved
            reader.ReadBytes(3);

            Platform = (ZiPatchConfig.PlatformId)reader.ReadUInt16BE();
            Region = (RegionId)reader.ReadInt16BE();
            IsDebug = reader.ReadInt16BE() != 0;
            Version = reader.ReadUInt16BE();
            DeletedDataSize = reader.ReadUInt64();
            SeekCount = reader.ReadUInt64();

            // Empty 32 + 64 bytes
            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            config.Platform = Platform;
        }

        public override string ToString()
        {
            return $"{Type}:{Command}:{Platform}:{Region}:{IsDebug}:{Version}:{DeletedDataSize}:{SeekCount}";
        }
    }
}