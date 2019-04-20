using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;

namespace XIVLauncher
{
    public static class Util
    {
        public static void ShowError(string message, string caption, [CallerMemberName] string callerName = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            MessageBox.Show($"{message}\n\n{callerName} L{callerLineNumber}", caption, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        
        /// <summary> Gets the git hash value from the assembly
        /// or null if it cannot be found. </summary>
        public static string GetGitHash()
        {
            var asm = typeof(Util).Assembly;
            var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
            return attrs.FirstOrDefault(a => a.Key == "GitHash")?.Value;
        }

        public static string GetAssemblyVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public static bool IsValidFFXIVPath(string path)
        {
            if (String.IsNullOrEmpty(path))
                return false;

            return Directory.Exists(Path.Combine(path, "game")) && Directory.Exists(Path.Combine(path, "boot"));
        }

        private static readonly string[] PathsToTry = 
        {
            "C:\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn",
            "C:\\Program Files (x86)\\Steam\\steamapps\\common\\FINAL FANTASY XIV Online",
            "C:\\Program Files (x86)\\Steam\\steamapps\\common\\FINAL FANTASY XIV - A Realm Reborn",
            "C:\\Program Files (x86)\\FINAL FANTASY XIV - A Realm Reborn"
        };

        public static string TryGamePaths()
        {
            foreach (var path in PathsToTry)
            {
                if (Directory.Exists(path) && IsValidFFXIVPath(path))
                    return path;
            }

            return null;
        }

        public static bool IsWindowsDarkModeEnabled()
        {
            return (int) Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", 0x1) == 0x0;
        }
    }
}