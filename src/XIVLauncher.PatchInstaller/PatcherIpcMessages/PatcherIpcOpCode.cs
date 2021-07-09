using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    public enum PatcherIpcOpCode
    {
        Hello,
        Bye,
        StartInstall,
        InstallRunning,
        InstallOk,
        InstallFailed,
        Finish
    }
}
