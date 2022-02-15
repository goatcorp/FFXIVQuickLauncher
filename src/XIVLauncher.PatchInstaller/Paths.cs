using System;
using System.IO;

namespace XIVLauncher
{
    /// <summary>
    /// Paths to various common folders.
    /// </summary>
    public class Paths
    {
        /// <summary>
        /// Gets the path to the AppData\Roaming\XIVLauncher folder.
        /// </summary>
        public static string RoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");

        /// <summary>
        /// Gets the path to the XIVLauncher resources folder.
        /// </summary>
        public static string ResourcesPath = Path.Combine(Path.GetDirectoryName(typeof(Paths).Assembly.Location), "Resources");
    }
}
