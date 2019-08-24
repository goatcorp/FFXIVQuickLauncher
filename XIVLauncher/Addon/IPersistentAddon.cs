using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Addon
{
    interface IPersistentAddon : IAddon
    { 
        Task DoWork(Process gameProcess, CancellationToken cancellationToken);
    }
}
