using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    /// <summary>
    /// Patcher IPC OpCodes.
    /// </summary>
    public enum PatcherIpcOpCode
    {
        /// <summary>
        /// Begin communications.
        /// </summary>
        Hello,

        /// <summary>
        /// End communications.
        /// </summary>
        Bye,

        /// <summary>
        /// Begin patch install.
        /// </summary>
        StartInstall,

        /// <summary>
        /// Patch install is running.
        /// </summary>
        InstallRunning,

        /// <summary>
        /// Patch install finished successfully.
        /// </summary>
        InstallOk,

        /// <summary>
        /// Patch install failed.
        /// </summary>
        InstallFailed,

        /// <summary>
        /// Finish patch install.
        /// </summary>
        Finish,
    }
}
