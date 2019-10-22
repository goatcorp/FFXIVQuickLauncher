using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Game.Patch.ZiPatch.Structures.Commands
{
    interface IZiPatchCommand
    {
        void Prepare(BinaryReader reader, long commandSize, ZiPatchExecute execute); 
        void Execute();
        bool CanExecute { get; }
    }
}
