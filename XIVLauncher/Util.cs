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
using IWshRuntimeLibrary;
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

        private static string DefaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SquareEnix\\FINAL FANTASY XIV - A Realm Reborn");

        private static readonly string[] PathsToTry = DriveInfo.GetDrives().Select(drive => $"{drive.Name}SquareEnix\\FINAL FANTASY XIV - A Realm Reborn").Concat(new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam\\steamapps\\common\\FINAL FANTASY XIV Online"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam\\steamapps\\common\\FINAL FANTASY XIV - A Realm Reborn"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "FINAL FANTASY XIV - A Realm Reborn"),
            DefaultPath
        }).ToArray();

        public static string TryGamePaths()
        {
            foreach (var path in PathsToTry)
                if (Directory.Exists(path) && IsValidFfxivPath(path))
                    return path;

            return DefaultPath;
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

        public static void StartOfficialLauncher(DirectoryInfo gamePath, bool isSteam)
        {
            Process.Start(Path.Combine(gamePath.FullName, "boot", "ffxivboot.exe"), isSteam ? "-issteam" : string.Empty);
        }

        public static void OpenDiscord(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/3NMcUV5");
        }

        public static bool IsWine => bool.Parse(Environment.GetEnvironmentVariable("XL_WINEONLINUX") ?? "false");

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

        public static string GetFromResources(string resourceName)
        {
            var asm = typeof(Util).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }

    }
}