using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using XIVLauncher.Encryption;
using XIVLauncher.Game;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for PatchHandlerWindow.xaml
    /// </summary>
    public partial class PatchHandlerWindow : Window
    {
        private IntPtr _windowHwnd;
        private readonly string _userPath;

        private const int GamePatchInstallListVersion = 2;

        public PatchHandlerWindow()
        {
            InitializeComponent();

            _userPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games",
                "FINAL FANTASY XIV - A Realm Reborn");
        }

        public new void Show()
        {
            base.Show();

            _windowHwnd = new WindowInteropHelper(this).Handle;
            var hwndSource = HwndSource.FromHwnd(_windowHwnd);
            hwndSource.AddHook(WndProcHandler);
        }

        private IntPtr WndProcHandler(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            Debug.WriteLine(
                $"[WNDPROC] hwnd:{hwnd.ToString("X")} msg:{msg.ToString("X")} wparam:{wparam.ToString("X")} lparam:{lparam.ToString("X")}");

            return IntPtr.Zero;
        }

        private void PrepareUpdater()
        {
            File.Copy(Path.Combine(Settings.GamePath.FullName, "boot", "ffxivupdater.exe"),
                Path.Combine(_userPath, "downloads", "ffxivupdater.exe"), true);
        }

        private void WriteGamePatchInstallList(string verName, string verFile, string patchFile)
        {
            File.WriteAllText(Path.Combine(_userPath, "downloads", "GamePatchInstall.list"),
                $"patch_list_version={GamePatchInstallListVersion}\r\n\"ver_name={verName}\" \"ver_file={verFile}\" \"patch_file={patchFile}\"\r\n");
        }

        private void LaunchUpdater()
        {
            var ticks = (uint) Environment.TickCount;
            var key = ticks & 0xFFFF_0000;

            var argumentBuilder = new ArgumentBuilder()
                .Append("T", ticks.ToString())
                .Append("BootVersion", XivGame.GetLocalBootVer())
                .Append("CallerWindow", _windowHwnd.ToString())
                .Append("GameVersion", XivGame.GetLocalGameVer())
                .Append("IsSteam", "0")
                .Append("NextExe", Path.Combine(Settings.GamePath.FullName, "game", "ffxiv.exe"))
                .Append("ShowMode", "2")
                .Append("UserPath", _userPath);

            Debug.WriteLine($"Launching ffxivupdater with key {key}: {argumentBuilder.Build()}");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(_userPath, "downloads", "ffxivupdater.exe"),
                    Arguments = argumentBuilder.BuildEncrypted(key)
                }
            };

            process.Start();
        }

        private void LaunchUpdaterButton_OnClick(object sender, RoutedEventArgs e)
        {
            PrepareUpdater();
            LaunchUpdater();
        }
    }
}