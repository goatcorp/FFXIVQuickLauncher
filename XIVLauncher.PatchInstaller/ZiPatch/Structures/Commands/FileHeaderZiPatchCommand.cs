using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands
{
    class FileHeaderZiPatchCommand : IZiPatchCommand
    {
        public byte PatchVersion { get; private set; }
        public string PatchType { get; private set; }

        void IZiPatchCommand.Handle(BinaryReader reader, long commandSize, ZiPatchExecute execute)
        {
            reader.BaseStream.Position += 2;

            PatchVersion = reader.ReadByte();

            reader.BaseStream.Position++;

            PatchType = Encoding.ASCII.GetString(reader.ReadBytes(4));

            reader.BaseStream.Position += 0xEC;
        }
    }
}
