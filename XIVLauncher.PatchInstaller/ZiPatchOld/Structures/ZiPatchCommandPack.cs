using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using XIVLauncher.PatchInstaller.Util;
using XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands;
using XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands.SqPack;

namespace XIVLauncher.PatchInstaller.ZiPatch.Structures
{
    class ZiPatchCommandPack
    {
        public ZiPatchCommandType CommandType { get; private set; }
        public IZiPatchCommand Command { get; private set; }

        public uint CommandSize { get; private set; }

        private readonly ZiPatchExecute _execute;

        public ZiPatchCommandPack(BinaryReader reader, ZiPatchExecute execute)
        {
            _execute = execute;

            Read(reader);
        }

        private void Read(BinaryReader binReader)
        {
            // Read chunk header
            CommandSize = binReader.ReadUInt32BE();
            //var reader = new Crc32BinaryReader(binReader.BaseStream);

            //var chunkIdentifier = Encoding.ASCII.GetString(reader.ReadBytes(4));

            //Log.Verbose("CHUNK: {0} - {1} - {2}", chunkIdentifier, CommandSize.ToString("X"), reader.BaseStream.Position.ToString("X"));

            //"FHDR".get
            /*
            switch (chunkIdentifier)
            {
                case "FHDR":
                    CommandType = ZiPatchCommandType.FileHeader;
                    Command = new FileHeaderZiPatchCommand();
                    break;
                case "APLY":
                    CommandType = ZiPatchCommandType.ApplyOption;
                    reader.BaseStream.Position += CommandSize;
                    break;
                case "APFS":
                    break;
                case "ETRY":
                    break;
                case "ADIR":
                    break;
                case "DELD":
                    break;
                case "SQPK":
                    CommandType = ZiPatchCommandType.SQPK;
                    Command = new SqPackZiPatchCommand();
                    break;
                case "EOF_":
                    CommandType = ZiPatchCommandType.EndOfFile;
                    reader.BaseStream.Position += CommandSize;
                    break;
                case "XXXX":
                    break;
                default:
                    throw new Exception("Unknown ZiPatch command type: " + chunkIdentifier);
            }*/

            //Command?.Handle(reader, CommandSize, _execute);

            // Read chunk CRC32
            /*var calculatedCrc = reader.GetCrc32();
            var fileCrc = reader.ReadUInt32BE();

            Log.Verbose($"/CHUNK: CRC32 {calculatedCrc} == {fileCrc}");
            if (calculatedCrc != fileCrc)
                throw new Exception("Command CRC Mismatch!");*/
        }
    }
}
