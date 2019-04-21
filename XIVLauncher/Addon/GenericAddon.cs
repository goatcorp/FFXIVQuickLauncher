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
            // If there already is a process like this running - we don't need to spawn another one.
            if (Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(Path)).Any())
                return;

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
