using System;
using System.IO;

namespace XIVLauncher
{
    public class Paths
    {
        public static string RoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");

        public static string ResourcesPath = Path.Combine(Path.GetDirectoryName(typeof(Paths).Assembly.Location), "Resources");
    }
}
