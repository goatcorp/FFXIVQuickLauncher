using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace XIVLauncher
{
    public static class AppUtil
    {
        /// <summary>
        ///     Gets the git hash value from the assembly
        ///     or null if it cannot be found.
        /// </summary>
        public static string GetGitHash()
        {
            var asm = typeof(AppUtil).Assembly;
            var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
            return attrs.FirstOrDefault(a => a.Key == "GitHash")?.Value;
        }

        /// <summary>
        ///     Gets the build origin from the assembly
        ///     or null if it cannot be found.
        /// </summary>
        public static string GetBuildOrigin()
        {
            var asm = typeof(AppUtil).Assembly;
            var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
            return attrs.FirstOrDefault(a => a.Key == "BuildOrigin")?.Value;
        }

        public static string GetAssemblyVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public static string GetFromResources(string resourceName)
        {
            var asm = typeof(AppUtil).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}