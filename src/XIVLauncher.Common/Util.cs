using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace XIVLauncher.Common
{
    /// <summary>
    /// Various utility methods.
    /// </summary>
    public static class Util
    {
        /// <summary>
        /// Generates a temporary file name.
        /// </summary>
        /// <returns>A temporary file name that is almost guaranteed to be unique.</returns>
        public static string GetTempFileName()
        {
            // https://stackoverflow.com/a/50413126
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Returns <see langword="true"/> if the current system region is set to North America.
        /// </summary>
        /// <returns>A value indicating if the current region is within North America.</returns>
        public static bool IsRegionNorthAmerica()
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName is "US" or "MX" or "CA";
        }

        /// <summary>
        /// Returns <see langwod="true"/> if a given path contains "game" and "boot" folders.
        /// </summary>
        /// <param name="path">Path to a directory.</param>
        /// <returns>A value indicating if the path is a valid FFXIV install.</returns>
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

        /// <summary>
        /// Gets the current time in epoch.
        /// </summary>
        /// <returns>The current time in unix milliseconds.</returns>
        public static long GetUnixMillis()
        {
            return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public static FileInfo GetOfficialLauncherPath(DirectoryInfo gamePath) => new(Path.Combine(gamePath.FullName, "boot", "ffxivboot.exe"));

        /// <summary>
        /// Starts the official SQEX launcher.
        /// </summary>
        /// <param name="gamePath">Path to the game install.</param>
        /// <param name="isSteam">A value indicating whether this is a Steam install.</param>
        public static void StartOfficialLauncher(DirectoryInfo gamePath, bool isSteam, bool isFreeTrial)
        {
            var args = string.Empty;

            if (isSteam && isFreeTrial)
            {
                args = "-issteamfreetrial";
            }
            else if (isSteam)
            {
                args = "-issteam";
            }

            Process.Start(GetOfficialLauncherPath(gamePath).FullName, args);
        }

        /// <summary>
        /// Convert a download speed to a string.
        /// </summary>
        /// <param name="byteCount">Value to convert.</param>
        /// <returns>The string representation.</returns>
        public static string BytesToString(double byteCount)
            => BytesToString(Convert.ToInt64(Math.Floor(byteCount)));

        /// <summary>
        /// Convert a download speed to a string.
        /// </summary>
        /// <param name="byteCount">Value to convert.</param>
        /// <returns>The string representation.</returns>
        public static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; // Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return $"{(Math.Sign(byteCount) * num):#0.0}{suf[place]}";
        }

        /// <summary>
        /// Check if the game is currently open.
        /// </summary>
        /// <returns>A value indicating whether the game is open.</returns>
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

        /// <summary>
        /// Get the available disk space on a given volume.
        /// </summary>
        /// <param name="path">Volume path.</param>
        /// <returns>The amount of space available.</returns>
        public static long GetDiskFreeSpace(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            FileInfo file = new FileInfo(path);
            DriveInfo drive = new DriveInfo(file.Directory.FullName);

            return drive.AvailableFreeSpace;
        }

        private static readonly IPEndPoint DefaultLoopbackEndpoint = new(IPAddress.Loopback, port: 0);

        /// <summary>
        /// Get an available TCP port.
        /// </summary>
        /// <returns>The port number.</returns>
        public static int GetAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(DefaultLoopbackEndpoint);
            return ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        /// <summary>
        /// Generate a random "Accept-Language" header value.
        /// </summary>
        /// <param name="asdf">Random seed.</param>
        /// <returns>An "Accept-Language" header value.</returns>
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

        /// <summary>
        /// Add an http header without validation.
        /// This throws if it fails.
        /// </summary>
        /// <param name="headers">Headers to add to.</param>
        /// <param name="key">Header key.</param>
        /// <param name="value">Header value.</param>
        public static void AddWithoutValidation(this HttpHeaders headers, string key, string value)
        {
            var res = headers.TryAddWithoutValidation(key, value);

            if (!res)
                throw new Exception($"Could not add header - {key}: {value}");
        }

        /// <summary>
        /// Get the current platform.
        /// </summary>
        /// <returns>The current platform.</returns>
        public static Platform GetPlatform()
        {
            if (EnvironmentSettings.IsWine)
                return Platform.Win32OnLinux;

            // TODO(goat): Add mac here, once it's merged

            return Platform.Win32;
        }

        public static string ToMangledSeBase64(byte[] input)
        {
            return Convert.ToBase64String(input)
                          .Replace('+', '-')
                          .Replace('/', '_')
                          .Replace('=', '*');
        }

        /// <summary>
        /// Create a hexdump of the provided bytes.
        /// </summary>
        /// <param name="bytes">The bytes to hexdump.</param>
        /// <param name="offset">The offset in the byte array to start at.</param>
        /// <param name="bytesPerLine">The amount of bytes to display per line.</param>
        /// <returns>The generated hexdump in string form.</returns>
        public static string ByteArrayToHex(byte[] bytes, int offset = 0, int bytesPerLine = 16)
        {
            if (bytes == null) return string.Empty;

            var hexChars = "0123456789ABCDEF".ToCharArray();

            var offsetBlock = 8 + 3;
            var byteBlock = offsetBlock + (bytesPerLine * 3) + ((bytesPerLine - 1) / 8) + 2;
            var lineLength = byteBlock + bytesPerLine + Environment.NewLine.Length;

            var line = (new string(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            var numLines = (bytes.Length + bytesPerLine - 1) / bytesPerLine;

            var sb = new StringBuilder(numLines * lineLength);

            for (var i = 0; i < bytes.Length; i += bytesPerLine)
            {
                var h = i + offset;

                line[0] = hexChars[(h >> 28) & 0xF];
                line[1] = hexChars[(h >> 24) & 0xF];
                line[2] = hexChars[(h >> 20) & 0xF];
                line[3] = hexChars[(h >> 16) & 0xF];
                line[4] = hexChars[(h >> 12) & 0xF];
                line[5] = hexChars[(h >> 8) & 0xF];
                line[6] = hexChars[(h >> 4) & 0xF];
                line[7] = hexChars[(h >> 0) & 0xF];

                var hexColumn = offsetBlock;
                var charColumn = byteBlock;

                for (var j = 0; j < bytesPerLine; j++)
                {
                    if (j > 0 && (j & 7) == 0) hexColumn++;

                    if (i + j >= bytes.Length)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        var by = bytes[i + j];
                        line[hexColumn] = hexChars[(by >> 4) & 0xF];
                        line[hexColumn + 1] = hexChars[by & 0xF];
                        line[charColumn] = by < 32 ? '.' : (char)by;
                    }

                    hexColumn += 3;
                    charColumn++;
                }

                sb.Append(line);
            }

            return sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());
        }
    }
}
