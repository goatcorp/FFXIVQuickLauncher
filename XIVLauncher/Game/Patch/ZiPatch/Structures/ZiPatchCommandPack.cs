using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using XIVLauncher.Game.Patch.ZiPatch.Structures.Commands;
using XIVLauncher.Helpers;

namespace XIVLauncher.Game.Patch.ZiPatch.Structures
{
    class ZiPatchCommandPack
    {
        public ZiPatchCommandType CommandType { get; private set; }
        public IZiPatchCommand Command { get; private set; }

        public uint ChunkSize { get; private set; }

        private readonly ZiPatchExecute _execute;

        public ZiPatchCommandPack(BinaryReader reader, ZiPatchExecute execute)
        {
            _execute = execute;

            Read(reader);
        }

        private void Read(BinaryReader reader)
        {
            // Read chunk header
            ChunkSize = reader.ReadUInt32BE();
            var chunkIdentifier = Encoding.ASCII.GetString(reader.ReadBytes(4));

            Log.Verbose("CHUNK: {0} - {1} - {2}", chunkIdentifier, ChunkSize.ToString("X"), reader.BaseStream.Position.ToString("X"));

            switch (chunkIdentifier)
            {
                case "FHDR":
                    CommandType = ZiPatchCommandType.FileHeader;
                    Command = new FileHeaderZiPatchCommand();
                    reader.BaseStream.Position += ChunkSize;
                    break;
                case "APLY":
                    CommandType = ZiPatchCommandType.APLY;
                    reader.BaseStream.Position += ChunkSize;
                    break;
                case "SQPK":
                    CommandType = ZiPatchCommandType.SQPK;
                    reader.BaseStream.Position += ChunkSize;
                    break;
                case "EOF_":
                    CommandType = ZiPatchCommandType.EndOfFile;
                    reader.BaseStream.Position += ChunkSize;
                    break;
            }

            // Read chunk footer
            reader.ReadUInt32();

            Command?.Prepare(reader, _execute);
        }
    }
}
