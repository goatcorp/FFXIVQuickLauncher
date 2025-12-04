using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Game
{
    public static class IntegrityCheck
    {
        public const string INTEGRITY_CHECK_BASE_URL = "https://goatcorp.github.io/integrity/";

        public class IntegrityCheckData
        {
            public Dictionary<string, string> Hashes { get; set; } = null!;
            public string GameVersion { get; set; } = null!;
            public string LastGameVersion { get; set; } = null!;
        }

        public class IntegrityCheckProgress
        {
            public string CurrentFile { get; set; } = null!;
        }

        public enum CompareResult
        {
            Valid,
            Invalid,
            ReferenceNotFound,
            ReferenceFetchFailure,
        }

        public static async Task<(CompareResult compareResult, string? report, IntegrityCheckData? remoteIntegrity)>
            CompareIntegrityAsync(IProgress<IntegrityCheckProgress> progress, DirectoryInfo gamePath, bool onlyIndex = false)
        {
            IntegrityCheckData remoteIntegrity;

            try
            {
                remoteIntegrity = await DownloadIntegrityCheckForVersion(Repository.Ffxiv.GetVer(gamePath));
            }
            catch (WebException e)
            {
                if (e.Response is HttpWebResponse resp && resp.StatusCode == HttpStatusCode.NotFound)
                    return (CompareResult.ReferenceNotFound, null, null);

                return (CompareResult.ReferenceFetchFailure, null, null);
            }

            var reportBuilder = new StringBuilder();
            var failed = false;

            await Task.Run(() =>
            {
                foreach (var hashEntry in remoteIntegrity.Hashes)
                {
                    var relativePath = hashEntry.Key;

                    if (onlyIndex && (!relativePath.EndsWith(".index", StringComparison.Ordinal) && !relativePath.EndsWith(".index2", StringComparison.Ordinal)))
                        continue;

                    // Normalize to platform path separators and drop a leading backslash if present
                    var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    if (normalized.StartsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                        normalized = normalized.Substring(1);

                    // Combine with gamePath
                    var fullPath = Path.Combine(gamePath.FullName, normalized);
                    var fileInfo = new FileInfo(fullPath);

                    if (!fileInfo.Exists)
                    {
                        reportBuilder.AppendLine($"Missing: {relativePath}");
                        failed = true;
                        continue;
                    }

                    try
                    {
                        using (var sha1 = SHA1.Create())
                        using (var stream = new BufferedStream(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite), 1200000))
                        {
                            var hash = sha1.ComputeHash(stream);
                            var localHash = BitConverter.ToString(hash).Replace('-', ' ');

                            // report progress on the calling thread
                            progress?.Report(new IntegrityCheckProgress { CurrentFile = relativePath });

                            if (!string.Equals(localHash, hashEntry.Value, StringComparison.Ordinal))
                            {
                                reportBuilder.AppendLine($"Mismatch: {relativePath}");
                                failed = true;
                            }
                        }
                    }
                    catch (IOException)
                    {
                        reportBuilder.AppendLine($"Error reading: {relativePath}");
                        failed = true;
                    }
                }
            });

            return (failed ? CompareResult.Invalid : CompareResult.Valid, reportBuilder.ToString(), remoteIntegrity);
        }

        public static async Task<IntegrityCheckData> DownloadIntegrityCheckForVersion(string gameVersion)
        {
            using (var client = new HttpClient())
            {
                var json = await client.GetStringAsync(INTEGRITY_CHECK_BASE_URL + gameVersion + ".json");
                var result = JsonSerializer.Deserialize<IntegrityCheckData>(json);
                return result ?? throw new InvalidOperationException("Failed to deserialize integrity JSON");
            }
        }

        public static IntegrityCheckData GenerateIntegrityReport(
            DirectoryInfo gamePath, IProgress<IntegrityCheckProgress>? progress, bool onlyIndex = false)
        {
            var hashes = new Dictionary<string, string>();

            using (var sha1 = SHA1.Create())
            {
                CrawlDirectory(gamePath, sha1, gamePath, ref hashes, progress, onlyIndex);
            }

            return new IntegrityCheckData
            {
                GameVersion = Repository.Ffxiv.GetVer(gamePath),
                Hashes = hashes
            };
        }

        private static void CrawlDirectory(
            DirectoryInfo directory, SHA1 sha1, DirectoryInfo rootDirectory,
            ref Dictionary<string, string> results, IProgress<IntegrityCheckProgress>? progress, bool onlyIndex = false)
        {
            foreach (var file in directory.GetFiles())
            {
                var relativePath = file.FullName.Substring(rootDirectory.FullName.Length);

                // for unix compatibility with windows-generated integrity files.
                relativePath = relativePath.Replace("/", "\\");

                if (!relativePath.StartsWith("\\", StringComparison.Ordinal))
                    relativePath = "\\" + relativePath;

                if (!relativePath.StartsWith("\\game", StringComparison.Ordinal))
                    continue;

                if (onlyIndex && (!relativePath.EndsWith(".index", StringComparison.Ordinal) && !relativePath.EndsWith(".index2", StringComparison.Ordinal)))
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
                    CrawlDirectory(dir, sha1, rootDirectory, ref results, progress, onlyIndex);
            }
        }
    }
}
