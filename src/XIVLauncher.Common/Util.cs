using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace XIVLauncher.Common
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

        /// <summary>
        ///     Returns <see langword="true"/> if the current system region is set to North America.
        /// </summary>
        public static bool IsRegionNorthAmerica()
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName is "US" or "MX" or "CA";
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

        public static long GetUnixMillis()
        {
            return (long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
        }

        public static void StartOfficialLauncher(DirectoryInfo gamePath, bool isSteam)
        {
            Process.Start(Path.Combine(gamePath.FullName, "boot", "ffxivboot.exe"), isSteam ? "-issteam" : string.Empty);
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