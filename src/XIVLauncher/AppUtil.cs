using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using XIVLauncher.Common;

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

        private static readonly string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SquareEnix\\FINAL FANTASY XIV - A Realm Reborn");

        private static string[] GetCommonPaths()
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var paths = new List<string>();
            var drives = DriveInfo.GetDrives().Select(info => info.Name);

            var commonPaths = new string[]
            {
                "Steam\\steamapps\\common\\FINAL FANTASY XIV Online",
                "Steam\\steamapps\\common\\FINAL FANTASY XIV - A Realm Reborn",
                "SquareEnix\\FINAL FANTASY XIV - A Realm Reborn",
                "Square Enix\\FINAL FANTASY XIV - A Realm Reborn",
                "Games\\SquareEnix\\FINAL FANTASY XIV - A Realm Reborn",
                "Games\\Square Enix\\FINAL FANTASY XIV - A Realm Reborn",
            };

            foreach (var commonPath in commonPaths)
            {
                paths.Add(Path.Combine(programFiles, commonPath));

                foreach (var drive in drives)
                {
                    paths.Add(Path.Combine(drive, commonPath));
                    paths.Add(Path.Combine(drive, "Program Files (x86)", commonPath));
                }
            }

            paths.Add(Path.Combine(programFiles, "FINAL FANTASY XIV - A Realm Reborn"));

            return paths.ToArray();
        }

        private static readonly int[] ValidSteamAppIds = new int[] {
            39210, // Paid version
            312060, // Free trial version
        };

        public static string TryGamePaths()
        {
            try
            {
                var foundVersions = new Dictionary<string, SeVersion>();

                foreach (var path in GetCommonPaths())
                {
                    if (!Directory.Exists(path) || !Util.IsValidFfxivPath(path) || foundVersions.ContainsKey(path))
                        continue;

                    var baseVersion = Repository.Ffxiv.GetVer(new DirectoryInfo(path));
                    foundVersions.Add(path, SeVersion.Parse(baseVersion));
                }

                foreach (var registryView in new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
                    {
                        // Should return "C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\boot\ffxivboot.exe" if installed with default options.
                        using (var subkey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{2B41E132-07DF-4925-A3D3-F2D1765CCDFE}"))
                        {
                            if (subkey != null && subkey.GetValue("DisplayIcon", null) is string path)
                            {
                                // DisplayIcon includes "boot\ffxivboot.exe", need to remove it
                                path = Directory.GetParent(path).Parent.FullName;

                                if (Directory.Exists(path) && Util.IsValidFfxivPath(path) && !foundVersions.ContainsKey(path))
                                {
                                    var baseVersion = Repository.Ffxiv.GetVer(new DirectoryInfo(path));
                                    foundVersions.Add(path, SeVersion.Parse(baseVersion));
                                }
                            }
                        }

                        // Should return "C:\Program Files (x86)\Steam\steamapps\common\FINAL FANTASY XIV Online" if installed with default options.
                        foreach (var steamAppId in ValidSteamAppIds)
                        {
                            using (var subkey = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {steamAppId}"))
                            {
                                if (subkey != null && subkey.GetValue("InstallLocation", null) is string path)
                                {
                                    if (Directory.Exists(path) && Util.IsValidFfxivPath(path) && !foundVersions.ContainsKey(path))
                                    {
                                        // InstallLocation is the root path of the game (the one containing boot and game) itself
                                        var baseVersion = Repository.Ffxiv.GetVer(new DirectoryInfo(path));
                                        foundVersions.Add(path, SeVersion.Parse(baseVersion));
                                    }
                                }
                            }
                        }

                    }
                }

                return foundVersions.Count == 0 ? defaultPath : foundVersions.OrderByDescending(x => x.Value).First().Key;
            }
            catch (Exception)
            {
                return defaultPath;
            }
        }
    }
}