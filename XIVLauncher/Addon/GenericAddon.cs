using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Addon
{
    public class GenericAddon : IAddon
    {
        public void Run()
        {
            var process = new Process {StartInfo = {FileName = Path, Arguments = CommandLine}};

            if (RunAsAdmin)
            {
                // Vista or higher check
                // https://stackoverflow.com/a/2532775
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    process.StartInfo.Verb = "runas";
                }
            }

            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

            process.Start();
        }

        public string Name => "Launch EXE: " + System.IO.Path.GetFileNameWithoutExtension(Path);

        public string Path;
        public string CommandLine;
        public bool RunAsAdmin;
    }
}
