using System.IO;

namespace XIVLauncher.Common.PatcherIpc
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