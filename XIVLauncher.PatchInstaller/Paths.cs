using System;
using System.IO;

namespace XIVLauncher
{
    public class Paths
    {
        public static string RoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher");
    }
}
