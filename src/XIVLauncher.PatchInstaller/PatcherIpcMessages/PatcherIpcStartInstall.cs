using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.PatchInstaller.PatcherIpcMessages
{
    /// <summary>
    /// Patcher IPC <see cref="PatcherIpcOpCode.StartInstall"/> payload.
    /// </summary>
    public class PatcherIpcStartInstall
    {
        /// <summary>
        /// Gets or sets the patch file.
        /// </summary>
        public FileInfo PatchFile { get; set; }

        /// <summary>
        /// Gets or sets the repository.
        /// </summary>
        public Repository Repo { get; set; }

        /// <summary>
        /// Gets or sets the version ID.
        /// </summary>
        public string VersionId { get; set; }

        /// <summary>
        /// Gets or sets the game directory.
        /// </summary>
        public DirectoryInfo GameDirectory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to keep the patch afterwards.
        /// </summary>
        public bool KeepPatch { get; set; }
    }
}
