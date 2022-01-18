using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Serilog;
using XIVLauncher.Game;
using XIVLauncher.PatchInstaller;

namespace XIVLauncher
{
    public static class Util
    {
        /// <summary>
        ///     Generates a temporary file name.
        /// </summary>
        /// <returns>A temporary file name that is almost guaranteed to be unique.</returns>
        public static string GetTempFileName()
        {
            // https://stackoverflow.com/a/50413126
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

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

        /// <summary>
        ///     Gets the build origin from the assembly
        ///     or null if it cannot be found.
        /// </summary>
        public static string GetBuildOrigin()
        {
            var asm = typeof(Util).Assembly;
            var attrs = asm.GetCustomAttributes<AssemblyMetadataAttribute>();
            return attrs.FirstOrDefault(a => a.Key == "BuildOrigin")?.Value;
        }

        /// <summary>
        ///     Returns <see langword="true"/> if the current system region is set to North America.
        /// </summary>
        public static bool IsRegionNorthAmerica()
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName is "US" or "MX" or "CA";
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

        public static bool LetChoosePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            var di = new DirectoryInfo(path);

            if (di.Name == "game")
                return false;

            if (di.Name == "boot")
                return false;

            if (di.Name == "sqpack")
                return false;

            return true;
        }

        private static string DefaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SquareEnix\\FINAL FANTASY XIV - A Realm Reborn");

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
            39210 /* Paid version */,
            312060, /* Free trial version */ 
        };

        public static string TryGamePaths()
        {
            try
            {
                var foundVersions = new Dictionary<string, SeVersion>();

                foreach (var path in GetCommonPaths())
                {
                    if (!Directory.Exists(path) || !IsValidFfxivPath(path) || foundVersions.ContainsKey(path))
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

                                if (Directory.Exists(path) && IsValidFfxivPath(path) && !foundVersions.ContainsKey(path))
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
                                    if (Directory.Exists(path) && IsValidFfxivPath(path) && !foundVersions.ContainsKey(path))
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

                return foundVersions.Count == 0 ? DefaultPath : foundVersions.OrderByDescending(x => x.Value).First().Key;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not search for game paths");
                return DefaultPath;
            }
        }

        public static long GetUnixMillis()
        {
            return (long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
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

        public static void OpenFaq(object sender, RoutedEventArgs e)
        {
            Process.Start("https://goatcorp.github.io/faq/");
        }

        public static string BytesToString(double byteCount) => BytesToString(Convert.ToInt64(Math.Floor(byteCount)));

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
#if DEBUG
            return false;
#endif

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

        private static readonly IPEndPoint DefaultLoopbackEndpoint = new(IPAddress.Loopback, port: 0);

        public static int GetAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(DefaultLoopbackEndpoint);
            return ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        public static string GenerateAcceptLanguage(int asdf = 0)
        {
            var codes = new string[] { "de-DE", "en-US", "ja" };
            var codesMany = new string[] { "de-DE", "en-US,en", "en-GB,en", "fr-BE,fr", "ja", "fr-FR,fr", "fr-CH,fr" };
            var rng = new Random(asdf);

            var many = rng.Next(10) < 3;
            if (many)
            {
                var howMany = rng.Next(2, 4);
                var deck = codesMany.OrderBy((x) => rng.Next()).Take(howMany).ToArray();

                var hdr = string.Empty;
                for (int i = 0; i < deck.Count(); i++)
                {
                    hdr += deck.ElementAt(i) + $";q=0.{10 - (i + 1)}";

                    if (i != deck.Length - 1)
                        hdr += ";";
                }

                return hdr;
            }

            return codes[rng.Next(0, codes.Length)];
        }

        public static void AddWithoutValidation(this HttpHeaders headers, string key, string value)
        {
            var res = headers.TryAddWithoutValidation(key, value);

            if (!res)
                throw new Exception($"Could not add header - {key}: {value}");
        }

        public static Platform GetPlatform()
        {
            if (EnvironmentSettings.IsWine)
                return Platform.Win32OnLinux;

            // TODO(goat): Add mac here, once it's merged

            return Platform.Win32;
        }
    }
}