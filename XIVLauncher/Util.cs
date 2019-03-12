using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

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

        public static bool IsValidFFXIVPath(string path)
        {
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
    }
}