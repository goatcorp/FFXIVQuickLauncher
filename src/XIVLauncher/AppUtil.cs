using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using XIVLauncher.Common;
using XIVLauncher.Common.Util;

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

        private static string GetDefaultPath(string companyName, string gameName) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), $"{companyName}\\{gameName}");

        private static string[] GetCommonPaths(string companyName1, string companyName2, string gameName, string rebootName)
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var paths = new List<string>();
            var drives = DriveInfo.GetDrives().Select(info => info.Name);

            var commonPaths = new string[]
            {
                $"Steam\\steamapps\\common\\{gameName} Online",
                $"Steam\\steamapps\\common\\{gameName} - {rebootName}",
                $"{companyName1}{companyName2}\\{gameName} - {rebootName}",
                $"\\{gameName} - {rebootName}",
                $"Games\\{companyName1}{companyName2}\\{gameName} - {rebootName}",
                $"Games\\{companyName1} {companyName2}\\{gameName} - {rebootName}",
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

            paths.Add(Path.Combine(programFiles, $"{gameName} - {rebootName}"));

            return paths.ToArray();
        }

        private static readonly int[] ValidSteamAppIds = new int[] {
            39210, // Paid version
            312060, // Free trial version
        };

        public static string TryGamePaths()
        {
            const string CN_1 = "Square";
            const string CN_2 = "Enix";
            const string GN = "FINAL FANTASY XIV";
            const string RN = "A Realm Reborn";

            var defaultPath = GetDefaultPath($"{CN_1}{CN_2}", $"{GN} - {RN}");

            try
            {
                var foundVersions = new Dictionary<string, SeVersion>();

                foreach (var path in GetCommonPaths(CN_1, CN_2, GN, RN))
                {
                    if (!Directory.Exists(path) || !GameHelpers.IsValidGamePath(path) || foundVersions.ContainsKey(path))
                        continue;

                    var baseVersion = Repository.Ffxiv.GetVer(new DirectoryInfo(path));
                    foundVersions.Add(path, SeVersion.Parse(baseVersion));
                }

                foreach (var registryView in new RegistryView[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
                    {
                        // Should return "C:\Program Files (x86)\company\game\boot\ffxivboot.exe" if installed with default options.
                        using (var subkey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{2B41E132-07DF-4925-A3D3-F2D1765CCDFE}"))
                        {
                            if (subkey != null && subkey.GetValue("DisplayIcon", null) is string path)
                            {
                                // DisplayIcon includes "boot\ffxivboot.exe", need to remove it
                                path = Directory.GetParent(path).Parent.FullName;

                                if (Directory.Exists(path) && GameHelpers.IsValidGamePath(path) && !foundVersions.ContainsKey(path))
                                {
                                    var baseVersion = Repository.Ffxiv.GetVer(new DirectoryInfo(path));
                                    foundVersions.Add(path, SeVersion.Parse(baseVersion));
                                }
                            }
                        }

                        // Should return "C:\Program Files (x86)\Steam\steamapps\common\game Online" if installed with default options.
                        foreach (var steamAppId in ValidSteamAppIds)
                        {
                            using (var subkey = hklm.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {steamAppId}"))
                            {
                                if (subkey != null && subkey.GetValue("InstallLocation", null) is string path)
                                {
                                    if (Directory.Exists(path) && GameHelpers.IsValidGamePath(path) && !foundVersions.ContainsKey(path))
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