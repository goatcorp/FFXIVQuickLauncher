using Serilog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace XIVLauncher.Common.Addon.Implementations
{
    public class GenericAddon : IRunnableAddon, INotifyAddonAfterClose
    {
        private Process _addonProcess;

        void IAddon.Setup(int gamePid)
        {
        }

        public void Run() =>
            Run(false);

        private void Run(bool gameClosed)
        {
            if (string.IsNullOrEmpty(Path))
            {
                Log.Error("Generic addon path was null.");
                return;
            }

            if (RunOnClose && !gameClosed)
                return; // This Addon only runs when the game is closed.

            try
            {
                var ext = System.IO.Path.GetExtension(Path).ToLower();

                switch (ext)
                {
                    case ".ps1":
                        RunPowershell();
                        break;

                    case ".bat":
                        RunBatch();
                        break;

                    default:
                        RunApp();
                        break;
                }

                Log.Information("Launched addon {0}.", System.IO.Path.GetFileNameWithoutExtension(Path));
            }
            catch (Exception e)
            {
                Log.Error(e, "Could not launch generic addon.");
            }
        }

        public void GameClosed()
        {
            if (RunOnClose)
            {
                Run(true);
            }

            if (!RunAsAdmin)
            {
                try
                {
                    if (_addonProcess == null)
                        return;

                    if (_addonProcess.Handle == IntPtr.Zero)
                        return;

                    if (!_addonProcess.HasExited && KillAfterClose)
                    {
                        if (!_addonProcess.CloseMainWindow() || !_addonProcess.WaitForExit(1000))
                            _addonProcess.Kill();

                        _addonProcess.Close();
                    }
                }
                catch (Exception ex)
                {
                    Log.Information(ex, "Could not kill addon process.");
                }
            }
        }

        private void RunApp()
        {
            // If there already is a process like this running - we don't need to spawn another one.
            if (Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(Path)).Any())
            {
                Log.Information("Addon {0} is already running.", Name);
                return;
            }

            _addonProcess = new Process
            {
                StartInfo =
                {
                    FileName = Path,
                    Arguments = CommandLine,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(Path),
                },
            };

            if (RunAsAdmin)
                // Vista or higher check
                // https://stackoverflow.com/a/2532775
                if (Environment.OSVersion.Version.Major >= 6) _addonProcess.StartInfo.Verb = "runas";

            _addonProcess.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;

            _addonProcess.Start();
        }

        private void RunPowershell()
        {
            var ps = new ProcessStartInfo
            {
                FileName = Powershell,
                WorkingDirectory = System.IO.Path.GetDirectoryName(Path),
                Arguments = $@"-File ""{Path}"" {CommandLine}",
                UseShellExecute = false,
            };

            RunScript(ps);
        }

        private void RunBatch()
        {
            var ps = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec"),
                WorkingDirectory = System.IO.Path.GetDirectoryName(Path),
                Arguments = $@"/C ""{Path}"" {CommandLine}",
                UseShellExecute = false,
            };

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
                Log.Information("Launched addon {0}.", System.IO.Path.GetFileNameWithoutExtension(Path));
            }
            catch (Win32Exception exc)
            {
                // If the user didn't cause this manually by dismissing the UAC prompt, we throw it
                if ((uint)exc.HResult != 0x80004005)
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
        public bool RunOnClose;
        public bool KillAfterClose;

        private static readonly Lazy<string> LazyPowershell = new(GetPowershell);

        private static string Powershell => LazyPowershell.Value;

        private static string GetPowershell()
        {
            var result = "powershell.exe";

            var path = Environment.GetEnvironmentVariable("Path");
            var values = path?.Split(';') ?? Array.Empty<string>();

            foreach (var dir in values)
            {
                var powershell = System.IO.Path.Combine(dir, "pwsh.exe");
                if (File.Exists(powershell))
                {
                    result = powershell;
                    break;
                }
            }

            return result;
        }
    }
}