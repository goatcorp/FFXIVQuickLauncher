using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace XIVLauncher.Addon
{
    public class GenericAddon : IAddon
    {
        public void Run(Process gameProcess)
        {
            if (string.IsNullOrEmpty(this.Path))
            {
                Serilog.Log.Error("Generic addon path was null.");
                return;
            }

            var ext = System.IO.Path.GetExtension(this.Path).ToLower();

            switch (ext)
            {
                case ".ps1":
                    this.RunPwsh(gameProcess);
                    break;

                case ".bat":
                    this.RunBatch(gameProcess);
                    break;

                default:
                    this.RunApp(gameProcess);
                    break;
            }
        }

        private void RunApp(Process gameProcess)
        {
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
                Serilog.Log.Information("Launched addon {0}.", System.IO.Path.GetFileNameWithoutExtension(Path));
            }
            catch (Win32Exception exc)
            {
                // If the user didn't cause this manually by dismissing the UAC prompt, we throw it
                if ((uint)exc.HResult != 0x80004005)
                    throw;
            }
        }

        private void RunPwsh(Process gameProcess)
        {
            var ps = new ProcessStartInfo();

            ps.FileName = Pwsh;
            ps.WorkingDirectory = System.IO.Path.GetDirectoryName(this.Path);
            ps.Arguments = $@"-File ""{this.Path}"" {this.CommandLine}";
            ps.UseShellExecute = false;

            this.RunScript(ps);
        }

        private void RunBatch(Process gameProcess)
        {
            var ps = new ProcessStartInfo();

            ps.FileName = Environment.GetEnvironmentVariable("ComSpec");
            ps.WorkingDirectory = System.IO.Path.GetDirectoryName(this.Path);
            ps.Arguments = $@"/C ""{this.Path}"" {this.CommandLine}";
            ps.UseShellExecute = false;

            this.RunScript(ps);
        }

        private void RunScript(ProcessStartInfo ps)
        {
            ps.WindowStyle = ProcessWindowStyle.Hidden;
            ps.CreateNoWindow = true;

            if (RunAsAdmin)
            {
                // Vista or higher check
                // https://stackoverflow.com/a/2532775
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    ps.Verb = "runas";
                }
            }

            try
            {
                Process.Start(ps);
                Serilog.Log.Information("Launched addon {0}.", System.IO.Path.GetFileNameWithoutExtension(Path));
            }
            catch (Win32Exception exc)
            {
                // If the user didn't cause this manually by dismissing the UAC prompt, we throw it
                if ((uint)exc.HResult != 0x80004005)
                    throw;
            }
        }

        public string Name =>
            string.IsNullOrEmpty(this.Path) ?
            string.Empty :
            ($"Launch{(this.IsApp ? " EXE" : string.Empty)} : {System.IO.Path.GetFileNameWithoutExtension(Path)}");

        private bool IsApp =>
            !string.IsNullOrEmpty(this.Path) &&
            System.IO.Path.GetExtension(this.Path).ToLower() == ".exe";

        public string Path;
        public string CommandLine;
        public bool RunAsAdmin;

        private static readonly Lazy<string> LazyPwsh = new Lazy<string>(() => GetPwsh());

        private static string Pwsh => LazyPwsh.Value;

        private static string GetPwsh()
        {
            var result = "powershell.exe";

            var path = Environment.GetEnvironmentVariable("Path");
            var values = path.Split(';');

            foreach (var dir in values)
            {
                var pwsh = System.IO.Path.Combine(dir, "pwsh.exe");
                if (System.IO.File.Exists(pwsh))
                {
                    result = pwsh;
                    break;
                }
            }

            return result;
        }
    }
}
