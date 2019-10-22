using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Game.Patch.ZiPatch.Structures.Commands.SqPack
{
    enum SqPackCommandType
    {
        Unknown,
        Add,
        Delete,
        Expand,
        Header,
        File,
        PatchInfo,
        Index
    }
}
