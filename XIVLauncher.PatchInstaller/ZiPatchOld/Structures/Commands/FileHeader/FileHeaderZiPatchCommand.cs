using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands
{
    class FileHeaderZiPatchCommand : IZiPatchCommand
    {
        private const byte MAX_HEADER_SIZE = 0xF4;

        public byte PatchVersion { get; private set; }
        public string PatchType { get; private set; }

        public void Handle(BinaryReader reader, long commandSize, ZiPatchExecute execute)
        {
            if (commandSize > MAX_HEADER_SIZE)
            {
                Log.Debug($"Detected file header with size > {MAX_HEADER_SIZE}");

                throw new ArgumentException("Patch file is invalid or not ZiPatch.");
            }

            reader.ReadBytes(2);

            PatchVersion = reader.ReadByte();

            reader.ReadByte();

            PatchType = Encoding.ASCII.GetString(reader.ReadBytes(4));

            //reader.BaseStream.Position += 0xEC;
            reader.ReadBytes(0xEC);
        }
    }
}
