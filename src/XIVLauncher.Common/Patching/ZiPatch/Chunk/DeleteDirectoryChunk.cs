using System;
using System.Diagnostics;
using System.IO;
using Serilog;
using XIVLauncher.Common.Patching.Util;

namespace XIVLauncher.Common.Patching.ZiPatch.Chunk
{
    public class DeleteDirectoryChunk : ZiPatchChunk
    {
        public new static string Type = "DELD";

        public string DirName { get; protected set; }


        public DeleteDirectoryChunk(ChecksumBinaryReader reader, int offset, int size) : base(reader, offset, size) {}

        protected override void ReadChunk()
        {
            var start = reader.BaseStream.Position;

            var dirNameLen = reader.ReadUInt32BE();

            DirName = reader.ReadFixedLengthString(dirNameLen);

            reader.ReadBytes(Size - (int)(reader.BaseStream.Position - start));
        }

        public override void ApplyChunk(ZiPatchConfig config)
        {
            try
            {
                Directory.Delete(config.GamePath + DirName);
            }
            catch (Exception e)
            {
                Log.Debug(e, $"Ran into {this}, failed at deleting the dir.");
                throw;
            }
        }

        public override string ToString()
        {
            return $"{Type}:{DirName}";
        }
    }
}