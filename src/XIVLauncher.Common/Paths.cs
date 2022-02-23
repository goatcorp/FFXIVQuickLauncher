using System;
using System.IO;

namespace XIVLauncher.Common
{
    /// <summary>
    /// Paths to various commonly used files and folders.
    /// </summary>
    public class Paths
    {
        static Paths()
        {
            RoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");
        }

        /// <summary>
        /// Gets the path to the Windows AppData\Roaming path, or the current value if it has been overridden.
        /// </summary>
        public static string RoamingPath { get; private set; }

        /// <summary>
        /// Gets the path to the application resources folder.
        /// </summary>
        public static string ResourcesPath => Path.Combine(Path.GetDirectoryName(typeof(Paths).Assembly.Location), "Resources");

        /// <summary>
        /// Override the current roaming path.
        /// </summary>
        /// <param name="path">New path to use.</param>
        public static void OverrideRoamingPath(string path)
        {
            RoamingPath = path;
        }
    }
}
