using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Serilog;
using System.Security.Cryptography;
using System.Threading.Tasks;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Dalamud
{
    public class AssetManager
    {
        private const string ASSET_STORE_URL = "https://kamori.goats.dev/Dalamud/Asset/Meta";

        internal class AssetInfo
        {
            public int Version { get; set; }
            public IReadOnlyList<Asset> Assets { get; set; }

            public class Asset
            {
                public string Url { get; set; }
                public string FileName { get; set; }
                public string Hash { get; set; }
            }
        }

        public static async Task<DirectoryInfo> EnsureAssets(DirectoryInfo baseDir, bool forceProxy)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(4),
            };

            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
            };

            using var sha1 = SHA1.Create();

            Log.Verbose("[DASSET] Starting asset download");

            var (isRefreshNeeded, info) = CheckAssetRefreshNeeded(baseDir);

            // NOTE(goat): We should use a junction instead of copying assets to a new folder. There is no C# API for junctions in .NET Framework.

            var assetsDir = new DirectoryInfo(Path.Combine(baseDir.FullName, info.Version.ToString()));
            var devDir = new DirectoryInfo(Path.Combine(baseDir.FullName, "dev"));

            foreach (var entry in info.Assets)
            {
                var filePath = Path.Combine(assetsDir.FullName, entry.FileName);
                var filePathDev = Path.Combine(devDir.FullName, entry.FileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePathDev)!);
                }
                catch
                {
                    // ignored
                }

                var refreshFile = false;

                if (File.Exists(filePath) && !string.IsNullOrEmpty(entry.Hash))
                {
                    try
                    {
                        using var file = File.OpenRead(filePath);
                        var fileHash = sha1.ComputeHash(file);
                        var stringHash = BitConverter.ToString(fileHash).Replace("-", "");
                        refreshFile = stringHash != entry.Hash;
                        Log.Verbose("[DASSET] {0} has {1}, remote {2}", entry.FileName, stringHash, entry.Hash);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DASSET] Could not read asset");
                    }
                }

                if (!File.Exists(filePath) || isRefreshNeeded || refreshFile)
                {
                    var url = entry.Url;

                    if (forceProxy && url.Contains("/File/Get/"))
                    {
                        url = url.Replace("/File/Get/", "/File/GetProxy/");
                    }

                    Log.Verbose("[DASSET] Downloading {0} to {1}...", url, entry.FileName);

                    var request = await client.GetAsync(url).ConfigureAwait(true);
                    request.EnsureSuccessStatusCode();
                    File.WriteAllBytes(filePath, await request.Content.ReadAsByteArrayAsync().ConfigureAwait(true));

                    try
                    {
                        File.Copy(filePath, filePathDev, true);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (isRefreshNeeded)
                SetLocalAssetVer(baseDir, info.Version);

            Log.Verbose("[DASSET] Assets OK at {0}", assetsDir.FullName);

            CleanUpOld(baseDir, info.Version - 1);

            return assetsDir;
        }

        private static string GetAssetVerPath(DirectoryInfo baseDir)
        {
            return Path.Combine(baseDir.FullName, "asset.ver");
        }

        /// <summary>
        ///     Check if an asset update is needed. When this fails, just return false - the route to github
        ///     might be bad, don't wanna just bail out in that case
        /// </summary>
        /// <param name="baseDir">Base directory for assets</param>
        /// <returns>Update state</returns>
        private static (bool isRefreshNeeded, AssetInfo info) CheckAssetRefreshNeeded(DirectoryInfo baseDir)
        {
            using var client = new WebClient();

            var localVerFile = GetAssetVerPath(baseDir);
            var localVer = 0;

            try
            {
                if (File.Exists(localVerFile))
                    localVer = int.Parse(File.ReadAllText(localVerFile));
            }
            catch (Exception ex)
            {
                // This means it'll stay on 0, which will redownload all assets - good by me
                Log.Error(ex, "[DASSET] Could not read asset.ver");
            }

            var remoteVer = JsonConvert.DeserializeObject<AssetInfo>(client.DownloadString(ASSET_STORE_URL));

            Log.Verbose("[DASSET] Ver check - local:{0} remote:{1}", localVer, remoteVer.Version);

            var needsUpdate = remoteVer.Version > localVer;

            return (needsUpdate, remoteVer);
        }

        private static void SetLocalAssetVer(DirectoryInfo baseDir, int version)
        {
            try
            {
                var localVerFile = GetAssetVerPath(baseDir);
                File.WriteAllText(localVerFile, version.ToString());
            }
            catch (Exception e)
            {
                Log.Error(e, "[DASSET] Could not write local asset version");
            }
        }

        private static void CleanUpOld(DirectoryInfo baseDir, int version)
        {
            if (GameHelpers.CheckIsGameOpen())
                return;

            var toDelete = Path.Combine(baseDir.FullName, version.ToString());

            try
            {
                if (Directory.Exists(toDelete))
                    Directory.Delete(toDelete, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not clean up old assets");
            }
        }
    }
}