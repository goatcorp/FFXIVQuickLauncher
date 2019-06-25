using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Addon
{
    public class GenericAddon : IAddon
    {
        public void Run(Process gameProcess)
        {
            if (Path == null)
            {
                Serilog.Log.Error("Generic addon path was null.");
                return;
            }

            // If there already is a process like this running - we don't need to spawn another one.
            if (Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(Path)).Any())
            {
                Serilog.Log.Information("Addon {0} is already running.", Name);
                return;
            }

            var process = new Process
            {
                StartInfo =
                {
                    FileName = Path,
                    Arguments = CommandLine,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(Path),
                }
            };

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

            try
            {
                process.Start();
                Serilog.Log.Information("Launched addon {0}.", Name);
            }
            catch (Win32Exception exc)
            {
                // If the user didn't cause this manually by dismissing the UAC prompt, we throw it
                if (exc.HResult != 0x80004005)
                    throw;
            }
        }

        public string Name => "Launch EXE: " + System.IO.Path.GetFileNameWithoutExtension(Path);

        public string Path;
        public string CommandLine;
        public bool RunAsAdmin;
    }
}
