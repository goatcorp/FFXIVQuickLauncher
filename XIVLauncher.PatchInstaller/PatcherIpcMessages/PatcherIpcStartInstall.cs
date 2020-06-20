using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    public class PatcherIpcStartInstall
    {
        public FileInfo PatchFile { get; set; }
        public bool IsBootPatch { get; set; }
        public DirectoryInfo GameDirectory { get; set; }
    }
}
