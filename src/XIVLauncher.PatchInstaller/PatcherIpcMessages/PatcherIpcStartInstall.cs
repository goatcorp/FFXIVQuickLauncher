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
        public Repository Repo { get; set; }
        public string VersionId { get; set; }
        public DirectoryInfo GameDirectory { get; set; }
        public bool KeepPatch { get; set; }
    }
}