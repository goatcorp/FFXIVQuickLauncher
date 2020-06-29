using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using XIVLauncher.Game;

namespace XIVLauncher
{
    public static class Util
    {
        public static void ShowError(string message, string caption, [CallerMemberName] string callerName = "",
            [CallerLineNumber] int callerLineNumber = 0)
        {
            MessageBox.Show($"{message}\n\n{callerName} L{callerLineNumber}", caption, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        /// <summary>
        ///     Gets the git hash value from the assembly
        ///     or null if it cannot be found.
        /// </summary>
        public static string GetGitHash()
        {
            var asm = typeof(Util).Assembly;
            var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
            return attrs.FirstOrDefault(a => a.Key == "GitHash")?.Value;
        }

        public static string GetAssemblyVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public static bool IsValidFfxivPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return Directory.Exists(Path.Combine(path, "game")) && Directory.Exists(Path.Combine(path, "boot"));
        }

        private static readonly string[] PathsToTry =
        {
            "C:\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn",
            "C:\\Program Files (x86)\\Steam\\steamapps\\common\\FINAL FANTASY XIV Online",
            "C:\\Program Files (x86)\\Steam\\steamapps\\common\\FINAL FANTASY XIV - A Realm Reborn",
            "C:\\Program Files (x86)\\FINAL FANTASY XIV - A Realm Reborn",
            "C:\\Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn"
        };

        public static string TryGamePaths()
        {
            foreach (var path in PathsToTry)
                if (Directory.Exists(path) && IsValidFfxivPath(path))
                    return path;

            return "C:\\Program Files (x86)\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn";
        }

        public static int GetUnixMillis()
        {
            return (int) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public static Color ColorFromArgb(int argb)
        {
            return Color.FromArgb((byte) (argb >> 24), (byte) (argb >> 16), (byte) (argb >> 8), (byte) argb);
        }

        public static int ColorToArgb(Color color)
        {
            return (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
        }

        public static SolidColorBrush SolidColorBrushFromArgb(int argb)
        {
            return new SolidColorBrush(ColorFromArgb(argb));
        }

        private static Dictionary<int, string> _classJobFontDict = new Dictionary<int, string>
        {
            { 1, "\uF001" },
            { 2, "\uF002" },
            { 3, "\uF003" },
            { 4, "\uF004" },
            { 5, "\uF005" },
            { 6, "\uF006" },
            { 7, "\uF007" },
            { 8, "\uF008" },
            { 9, "\uF009" },
            { 10, "\uF010" },
            { 11, "\uF011" },
            { 12, "\uF012" },
            { 13, "\uF013" },
            { 14, "\uF014" },
            { 15, "\uF015" },
            { 16, "\uF016" },
            { 17, "\uF017" },
            { 18, "\uF018" },
            { 19, "\uF019" },
            { 20, "\uF020" },
            { 21, "\uF021" },
            { 22, "\uF022" },
            { 23, "\uF023" },
            { 24, "\uF024" },
            { 25, "\uF025" },
            { 26, "\uF026" },
            { 27, "\uF027" },
            { 28, "\uF028" },
            { 29, "\uF029" },
            { 30, "\uF030" },
            { 31, "\uF031" },
            { 32, "\uF032" },
            { 33, "\uF033" },
            { 34, "\uF034" },
            { 35, "\uF035" },
            { 36, "\uF036" },
            { 37, "\uF037" },
            { 38, "\uF038" }
        };

        public static string ClassJobToIcon(int classJob) => _classJobFontDict[classJob];

        public static void StartOfficialLauncher(DirectoryInfo gamePath, bool isSteam)
        {
            Process.Start(Path.Combine(gamePath.FullName, "boot", "ffxivboot.exe"), isSteam ? "-issteam" : string.Empty);
        }

        public static void OpenDiscord(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/3NMcUV5");
        }

        public static bool IsWine => Environment.GetEnvironmentVariable("XL_WINEONLINUX") == "True";

        public static string BytesToString(long byteCount)
        {
            string[] suf = {"B", "KB", "MB", "GB", "TB", "PB", "EB"}; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return $"{(Math.Sign(byteCount) * num):#0.0}{suf[place]}";
        }

        public static bool CheckIsGameOpen()
        {
            var procs = Process.GetProcesses();

            if (procs.Any(x => x.ProcessName == "ffxiv"))
                return true;

            if (procs.Any(x => x.ProcessName == "ffxiv_dx11"))
                return true;

            if (procs.Any(x => x.ProcessName == "ffxivboot"))
                return true;

            if (procs.Any(x => x.ProcessName == "ffxivlauncher"))
                return true;

            return false;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        public static ulong GetDiskFreeSpace(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException("path");
            }

            ulong dummy = 0;

            if (!GetDiskFreeSpaceEx(path, out ulong freeSpace, out dummy, out dummy))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return freeSpace;
        }
    }
}