using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    public class PatcherIpcEnvelope
    {
        public PatcherIpcOpCode OpCode { get; set; }
        public object Data { get; set; }
    }
}
