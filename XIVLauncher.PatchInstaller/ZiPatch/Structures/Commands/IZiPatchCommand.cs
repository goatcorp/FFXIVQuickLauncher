using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.ZiPatch.Structures.Commands
{
    interface IZiPatchCommand
    {
        void Handle(BinaryReader reader, long commandSize, ZiPatchExecute execute);
    }
}
