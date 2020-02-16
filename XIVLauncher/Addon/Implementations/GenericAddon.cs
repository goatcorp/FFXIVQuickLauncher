using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using XIVLauncher.Settings;

namespace XIVLauncher.Addon
{
    public class GenericAddon : IRunnableAddon, INotifyAddonAfterClose
    {
        private Process _addonProcess;
        private Process _gameProcess;
        
        void IAddon.Setup(Process gameProcess, ILauncherSettingsV3 setting)
        {
            _gameProcess = gameProcess;
        }

        public void Run()
        {
            if (string.IsNullOrEmpty(Path))
            {
                Serilog.Log.Error("Generic addon path was null.");
                return;
            }

            try
            {
                var ext = System.IO.Path.GetExtension(Path).ToLower();

                switch (ext)
                {
                    case ".ps1":
                        RunPwsh();
                        break;

                    case ".bat":
                        RunBatch();
                        break;

                    default:
                        RunApp();
                        break;
                }

                Serilog.Log.Information("Launched addon {0}.", System.IO.Path.GetFileNameWithoutExtension(Path));
            }
            catch (Exception e)
            {
                Serilog.Log.Error(e, "Could not launch generic addon.");
            }
        }

        public void GameClosed()
        {
            if (!RunAsAdmin)
            {
                try
                {
                    if (_addonProcess == null)
                        return;

                    if (_addonProcess.Handle == IntPtr.Zero)
                        return;

                    if (!_addonProcess.HasExited && KillAfterClose)
                        _addonProcess.Kill();
                }
                catch(Exception ex)
                {
                    Serilog.Log.Information(ex, "Could not kill addon process.");
                }
            }
        }

        private void RunApp()
        {
            // If there already is a process like this running - we don't need to spawn another one.
            if (Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(Path)).Any())
            {
                Serilog.Log.Information("Addon {0} is already running.", Name);
                return;
            }

            _addonProcess = new Process
            {
                StartInfo =
                {
                    FileName = Path,
                    Arguments = CommandLine,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(Path),
                }
            };

            if (RunAsAdmin)
            // Vista or higher check
            // https://stackoverflow.com/a/2532775
                if (Environment.OSVersion.Version.Major >= 6) _addonProcess.StartInfo.Verb = "runas";

            _addonProcess.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

            _addonProcess.Start();
        }

        private void RunPwsh()
        {
            var ps = new ProcessStartInfo();

            ps.FileName = Pwsh;
            ps.WorkingDirectory = System.IO.Path.GetDirectoryName(Path);
            ps.Arguments = $@"-File ""{Path}"" {CommandLine}";
            ps.UseShellExecute = false;

            RunScript(ps);
        }

        private void RunBatch()
        {
            var ps = new ProcessStartInfo();

            ps.FileName = Environment.GetEnvironmentVariable("ComSpec");
            ps.WorkingDirectory = System.IO.Path.GetDirectoryName(Path);
            ps.Arguments = $@"/C ""{Path}"" {CommandLine}";
            ps.UseShellExecute = false;

            RunScript(ps);
        }

        private void RunScript(ProcessStartInfo ps)
        {
            ps.WindowStyle = ProcessWindowStyle.Hidden;
            ps.CreateNoWindow = true;

            if (RunAsAdmin)
            // Vista or higher check
            // https://stackoverflow.com/a/2532775
                if (Environment.OSVersion.Version.Major >= 6) ps.Verb = "runas";

            try
            {
                _addonProcess = Process.Start(ps);
                Serilog.Log.Information("Launched addon {0}.", System.IO.Path.GetFileNameWithoutExtension(Path));
            }
            catch (Win32Exception exc)
            {
                // If the user didn't cause this manually by dismissing the UAC prompt, we throw it
                if ((uint) exc.HResult != 0x80004005)
                    throw;
            }
        }

        public string Name =>
            string.IsNullOrEmpty(Path)
                ? "Invalid addon"
                : $"Launch{(IsApp ? " EXE" : string.Empty)} : {System.IO.Path.GetFileNameWithoutExtension(Path)}";

        private bool IsApp =>
            !string.IsNullOrEmpty(Path) &&
            System.IO.Path.GetExtension(Path).ToLower() == ".exe";

        public string Path;
        public string CommandLine;
        public bool RunAsAdmin;
        public bool KillAfterClose;

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