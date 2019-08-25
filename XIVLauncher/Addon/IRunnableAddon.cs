using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Addon
{
    interface IRunnableAddon : IAddon
    {
        void Run();
    }
}
