using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Game
{
    public static class IntegrityCheck
    {
        public const string INTEGRITY_CHECK_BASE_URL = "https://goatcorp.github.io/integrity/";

        private const string ExpansionDirPrefix = "ex";

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
            ValidMissingExpansions,
            Invalid,
            ReferenceNotFound,
            ReferenceFetchFailure,
        }

        public static async Task<(CompareResult compareResult, string? report, IntegrityCheckData? remoteIntegrity)>
            CompareIntegrityAsync(HttpClient client, IProgress<IntegrityCheckProgress>? progress, DirectoryInfo gamePath, bool onlyIndex = false, CancellationToken cancellationToken = default)
        {
            IntegrityCheckData remoteIntegrity;

            try
            {
                remoteIntegrity = await DownloadIntegrityCheckForVersion(client, Repository.Ffxiv.GetVer(gamePath), cancellationToken);
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    return (CompareResult.ReferenceNotFound, null, null);
                }

                return (CompareResult.ReferenceFetchFailure, null, null);
            }

            var reportBuilder = new StringBuilder();
            var failed = false;

            var missingExpansions = remoteIntegrity.Hashes.Keys
                .Select(key => GetExpansionDir(NormalizePath(key)))
                .OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(dir => !Directory.Exists(Path.Combine(gamePath.FullName, dir)))
                .ToList();

            var skippedPrefixes = missingExpansions.Select(dir => dir + Path.DirectorySeparatorChar).ToList();

            foreach (var dir in missingExpansions)
            {
                reportBuilder.AppendLine($"Not installed: {Path.GetFileName(dir)}");
            }

            await Task.Run(() =>
            {
                foreach (var hashEntry in remoteIntegrity.Hashes)
                {
                    var relativePath = hashEntry.Key;

                    if (onlyIndex && (!relativePath.EndsWith(".index", StringComparison.Ordinal) && !relativePath.EndsWith(".index2", StringComparison.Ordinal)))
                        continue;

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var normalized = NormalizePath(relativePath);

                    if (skippedPrefixes.Any(prefix => normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

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
            }, cancellationToken);

            if (failed)
            {
                return (CompareResult.Invalid, reportBuilder.ToString(), remoteIntegrity);
            }

            var result = missingExpansions.Count > 0 ? CompareResult.ValidMissingExpansions : CompareResult.Valid;
            return (result, reportBuilder.ToString(), remoteIntegrity);
        }

        private static string NormalizePath(string relativePath)
        {
            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (normalized.StartsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                normalized = normalized.Substring(1);

            return normalized;
        }

        // "game\sqpack\ex5\0c0500.win32.index" to "game\sqpack\ex5"
        private static string? GetExpansionDir(string normalizedPath)
        {
            var parts = normalizedPath.Split(Path.DirectorySeparatorChar);
            if (parts.Length < 4)
            {
                return null;
            }

            var isSqpack = parts[0].Equals("game", StringComparison.OrdinalIgnoreCase) && parts[1].Equals("sqpack", StringComparison.OrdinalIgnoreCase);
            if (!isSqpack || !parts[2].StartsWith(ExpansionDirPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.Combine(parts[0], parts[1], parts[2]);
        }

        public static async Task<IntegrityCheckData> DownloadIntegrityCheckForVersion(HttpClient client, string gameVersion, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, INTEGRITY_CHECK_BASE_URL + gameVersion + ".json");
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<IntegrityCheckData>(await response.Content.ReadAsStringAsync(cancellationToken));
            return result ?? throw new InvalidOperationException("Failed to deserialize integrity JSON");
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
