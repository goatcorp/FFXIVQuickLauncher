using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Game.Patch.ZiPatch.Structures.Commands
{
    class FileHeaderZiPatchCommand : IZiPatchCommand
    {
        bool IZiPatchCommand.CanExecute => false;

        void IZiPatchCommand.Prepare(BinaryReader reader, ZiPatchExecute execute)
        {
            // throw new NotImplementedException();
        }

        void IZiPatchCommand.Execute()
        {
            throw new NotImplementedException();
        }
    }
}
