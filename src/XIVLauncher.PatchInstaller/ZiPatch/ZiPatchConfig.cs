using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XIVLauncher.PatchInstaller.ZiPatch.Util;

namespace XIVLauncher.PatchInstaller.ZiPatch
{
    /// <summary>
    /// ZiPatch configuration.
    /// </summary>
    public class ZiPatchConfig
    {
        /// <summary>
        /// Game platform IDs.
        /// </summary>
        public enum PlatformId : ushort
        {
            /// <summary>
            /// Windows.
            /// </summary>
            Win32 = 0,

            /// <summary>
            /// PlayStation 3.
            /// </summary>
            Ps3 = 1,

            /// <summary>
            /// PlayStation 4.
            /// </summary>
            Ps4 = 2,

            /// <summary>
            /// Unknown.
            /// </summary>
            Unknown = 3,
        }

        /// <summary>
        /// Gets the game path.
        /// </summary>
        public string GamePath { get; protected set; }

        /// <summary>
        /// Gets or sets the game platform.
        /// </summary>
        public PlatformId Platform { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to ignore missing files.
        /// </summary>
        public bool IgnoreMissing { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to ignore old mismatch files.
        /// </summary>
        public bool IgnoreOldMismatch { get; set; }

        /// <summary>
        /// Gets or sets the filestream store.
        /// </summary>
        public SqexFileStreamStore Store { get; set; }


        public ZiPatchConfig(string gamePath)
        {
            GamePath = gamePath;
        }
    }
}
