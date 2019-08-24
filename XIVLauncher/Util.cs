using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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

            return null;
        }

        public static bool IsWindowsDarkModeEnabled()
        {
            try
            {
                return (int) Registry.GetValue(
                           "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                           "AppsUseLightTheme", 0x1) == 0x0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAdministrator()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);
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

        // https://stackoverflow.com/questions/10454519/best-way-to-compare-two-complex-objects
        public static bool DeepCompare(this object obj, object another)
        {
            if (ReferenceEquals(obj, another)) return true;
            if (obj == null || another == null) return false;
            //Compare two object's class, return false if they are difference
            if (obj.GetType() != another.GetType()) return false;

            var result = true;
            //Get all properties of obj
            //And compare each other
            foreach (var property in obj.GetType().GetProperties())
            {
                var objValue = property.GetValue(obj);
                var anotherValue = property.GetValue(another);
                if (!objValue.Equals(anotherValue)) result = false;
            }

            return result;
        }

        public static bool CompareEx(this object obj, object another)
        {
            if (ReferenceEquals(obj, another)) return true;
            if (obj == null || another == null) return false;
            if (obj.GetType() != another.GetType()) return false;

            //properties: int, double, DateTime, etc, not class
            if (!obj.GetType().IsClass) return obj.Equals(another);

            var result = true;
            foreach (var property in obj.GetType().GetProperties())
            {
                var objValue = property.GetValue(obj);
                var anotherValue = property.GetValue(another);
                //Recursion
                if (!objValue.DeepCompare(anotherValue)) result = false;
            }

            return result;
        }
    }
}