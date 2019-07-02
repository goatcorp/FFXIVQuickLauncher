using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace XIVLauncher
{
    public static class IntegrityCheck
    {
        private const string IntegrityCheckBaseUrl = "https://goaaats.github.io/ffxiv/tools/launcher/integrity/";

        public class IntegrityCheckResult
        {
            public Dictionary<string, string> Hashes { get; set; }
            public string GameVersion { get; set; }
            public string LastGameVersion { get; set; }
        }

        public class IntegrityCheckProgress
        {
            public string CurrentFile { get; set; }
        }

        public enum CompareResult
        {
            Valid,
            Invalid,
            NoServer
        }

        public static async Task<(CompareResult compareResult, string report, IntegrityCheckResult remoteIntegrity)> CompareIntegrityAsync(IProgress<IntegrityCheckProgress> progress)
        {
            IntegrityCheckResult remoteIntegrity;

            try
            {
                remoteIntegrity = DownloadIntegrityCheckForVersion(XIVGame.GetLocalGamever());
            }
            catch (WebException)
            {
                return (CompareResult.NoServer, null, null);
            }
            
            var localIntegrity = await RunIntegrityCheckAsync(new DirectoryInfo(Settings.GetGamePath()), progress);

            var report = "";
            foreach (var hashEntry in remoteIntegrity.Hashes)
            {
                if (localIntegrity.Hashes.Any(h => h.Key == hashEntry.Key))
                {
                    if (localIntegrity.Hashes.First(h => h.Key == hashEntry.Key).Value != hashEntry.Value)
                    {
                        report += $"Mismatch: {hashEntry.Key}\n";
                    }
                }
                else
                {
                    Debug.WriteLine("File not found in local integrity: " + hashEntry.Key);
                }
            }

            return (string.IsNullOrEmpty(report) ? CompareResult.Valid : CompareResult.Invalid, report, remoteIntegrity);
        }

        private static IntegrityCheckResult DownloadIntegrityCheckForVersion(string gameVersion)
        {
            using (var client = new WebClient())
            {
                return JsonConvert.DeserializeObject<IntegrityCheckResult>(
                    client.DownloadString(IntegrityCheckBaseUrl + gameVersion + ".json"));
            }
        }

        private static async Task<IntegrityCheckResult> RunIntegrityCheckAsync(DirectoryInfo gameDirectory, IProgress<IntegrityCheckProgress> progress)
        {
            var hashes = new Dictionary<string, string>();

            using (var sha1 = new SHA1Managed())
            {
                CheckDirectory(gameDirectory, sha1, gameDirectory.FullName, ref hashes, progress);
            }

            return new IntegrityCheckResult
            {
                GameVersion = XIVGame.GetLocalGamever(),
                Hashes = hashes
            };
        }

        private static void CheckDirectory(DirectoryInfo directory, SHA1Managed sha1, string rootDirectory, ref Dictionary<string, string> results, IProgress<IntegrityCheckProgress> progress)
        {
            foreach (var file in directory.GetFiles())
            {
                var relativePath = file.FullName.Substring(rootDirectory.Length);

                if (!relativePath.StartsWith("\\game"))
                    continue;

                try
                {
                    using (var stream = new BufferedStream(file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite), 1200000))
                    {
                        var hash = sha1.ComputeHash(stream);

                        results.Add(relativePath, BitConverter.ToString(hash).Replace('-', ' '));

                        progress?.Report(new IntegrityCheckProgress
                        {
                            CurrentFile = relativePath
                        });

                        Debug.WriteLine(relativePath);
                    }
                }
                catch (IOException)
                {
                    // Ignore
                }
            }

            foreach (var dir in directory.GetDirectories())
            {
                CheckDirectory(dir, sha1, rootDirectory, ref results, progress);
            }
        }
    }
}
