using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game
{
    public static class IntegrityCheck
    {
        private const string INTEGRITY_CHECK_BASE_URL = "https://goatcorp.github.io/integrity/";

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
            ReferenceNotFound,
            ReferenceFetchFailure,
        }

        public static async Task<(CompareResult compareResult, string report, IntegrityCheckResult remoteIntegrity)>
            CompareIntegrityAsync(IProgress<IntegrityCheckProgress> progress, DirectoryInfo gamePath, bool onlyIndex = false)
        {
            IntegrityCheckResult remoteIntegrity;

            try
            {
                remoteIntegrity = DownloadIntegrityCheckForVersion(Repository.Ffxiv.GetVer(gamePath));
            }
            catch (WebException e)
            {
                if (e.Response is HttpWebResponse resp && resp.StatusCode == HttpStatusCode.NotFound)
                    return (CompareResult.ReferenceNotFound, null, null);
                return (CompareResult.ReferenceFetchFailure, null, null);
            }

            var localIntegrity = await RunIntegrityCheckAsync(gamePath, progress, onlyIndex);

            var report = "";
            var failed = false;
            foreach (var hashEntry in remoteIntegrity.Hashes)
            {
                if (onlyIndex && (!hashEntry.Key.EndsWith(".index") && !hashEntry.Key.EndsWith(".index2")))
                    continue;

                if (localIntegrity.Hashes.Any(h => h.Key == hashEntry.Key))
                {
                    if (localIntegrity.Hashes.First(h => h.Key == hashEntry.Key).Value != hashEntry.Value)
                    {
                        report += $"Mismatch: {hashEntry.Key}\n";
                        failed = true;
                    }
                }
                else
                {
                    report += $"Missing: {hashEntry.Key}\n";
                }
            }

            return (failed ? CompareResult.Invalid : CompareResult.Valid, report, remoteIntegrity);
        }

        private static IntegrityCheckResult DownloadIntegrityCheckForVersion(string gameVersion)
        {
            using (var client = new WebClient())
            {
                return JsonConvert.DeserializeObject<IntegrityCheckResult>(
                    client.DownloadString(INTEGRITY_CHECK_BASE_URL + gameVersion + ".json"));
            }
        }

        public static async Task<IntegrityCheckResult> RunIntegrityCheckAsync(DirectoryInfo gamePath,
            IProgress<IntegrityCheckProgress> progress, bool onlyIndex = false)
        {
#if DEBUG
            Log.Debug($"Platform identified as {PlatformHelpers.GetPlatform()}");
#endif
            var hashes = new Dictionary<string, string>();

            using (var sha1 = new SHA1Managed())
            {
                CheckDirectory(gamePath, sha1, gamePath.FullName, ref hashes, progress, onlyIndex);
            }

            return new IntegrityCheckResult
            {
                GameVersion = Repository.Ffxiv.GetVer(gamePath),
                Hashes = hashes
            };
        }

        private static void CheckDirectory(DirectoryInfo directory, SHA1Managed sha1, string rootDirectory,
                                           ref Dictionary<string, string> results, IProgress<IntegrityCheckProgress> progress, bool onlyIndex = false)
        {
            foreach (var file in directory.GetFiles())
            {
                var relativePath = file.FullName.Substring(rootDirectory.Length);


#if DEBUG
                Log.Debug($"{relativePath} swapping to {relativePath.Replace("/", "\\")}");
#endif
                // for unix compatibility with windows-generated integrity files.
                relativePath = relativePath.Replace("/", "\\");


                if (!relativePath.StartsWith("\\"))
                    relativePath = "\\" + relativePath;

                if (!relativePath.StartsWith("\\game"))
                    continue;

                if (onlyIndex && (!relativePath.EndsWith(".index") && !relativePath.EndsWith(".index2")))
                    continue;

                try
                {
                    using (var stream =
                           new BufferedStream(file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite), 1200000))
                    {
                        var hash = sha1.ComputeHash(stream);

                        results.Add(relativePath, BitConverter.ToString(hash).Replace('-', ' '));

                        progress?.Report(new IntegrityCheckProgress
                        {
                            CurrentFile = relativePath
                        });
                    }
                }
                catch (IOException)
                {
                    // Ignore
                }
            }

            foreach (var dir in directory.GetDirectories())
            {
                if (!dir.FullName.ToLower().Contains("shade")) //skip gshade directories. They just waste cpu
                    CheckDirectory(dir, sha1, rootDirectory, ref results, progress, onlyIndex);
            }
        }
    }
}