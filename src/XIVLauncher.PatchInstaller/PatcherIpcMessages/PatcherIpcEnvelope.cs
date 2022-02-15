using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    /// <summary>
    /// Patcher IPC envelope.
    /// </summary>
    public class PatcherIpcEnvelope
    {
        /// <summary>
        /// Gets or sets the IPC opcode kind.
        /// </summary>
        public PatcherIpcOpCode OpCode { get; set; }

        /// <summary>
        /// Gets or sets the IPC data.
        /// </summary>
        public object Data { get; set; }
    }
}
