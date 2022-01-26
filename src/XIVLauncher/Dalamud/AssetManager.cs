using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Windows;
using System.Security.Cryptography;
using Castle.Core.Internal;
using NuGet;

namespace XIVLauncher.Dalamud
{
    internal class AssetManager
    {
        private const string ASSET_STORE_URL = "https://goatcorp.github.io/DalamudAssets/";

        internal class AssetInfo
        {
            public int Version { get; set; }
            public List<Asset> Assets { get; set; }

            public class Asset
            {
                public string Url { get; set; }
                public string FileName { get; set; }
                public string Hash { get; set; }
            }
        }

        public static bool TryEnsureAssets(DirectoryInfo baseDir, out DirectoryInfo assetsDir)
        {
            using var client = new WebClient();
            using var sha1 = SHA1.Create();

            Log.Verbose("[DASSET] Starting asset download");

            var (isRefreshNeeded, info) = CheckAssetRefreshNeeded(baseDir);

            if (info == null)
            {
                assetsDir = null;
                return false;
            }
            
            // NOTE(goat): We should use a junction instead of copying assets to a new folder. There is no C# API for junctions in .NET Framework.

            var newAssetsDir = new DirectoryInfo(Path.Combine(baseDir.FullName, info.Version.ToString()));
            var devDir = new DirectoryInfo(Path.Combine(baseDir.FullName, "dev"));
            
            foreach (var entry in info.Assets)
            {
                var filePath = Path.Combine(newAssetsDir.FullName, entry.FileName);
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
                if (File.Exists(filePath) && !entry.Hash.IsNullOrEmpty())
                {
                    try
                    {
                        using var file = File.OpenRead(filePath);
                        var fileHash = sha1.ComputeHash(file);
                        var stringHash = BitConverter.ToString(fileHash).Replace("-", "");
                        refreshFile = stringHash != entry.Hash;
                        Log.Verbose("[DASSET] {0} has hash {1} when remote asset has {2}.", entry.FileName, stringHash, entry.Hash);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DASSET] Could not read asset.");
                    }
                }

                if (!File.Exists(filePath) || isRefreshNeeded || refreshFile)
                {
                    Log.Verbose("[DASSET] Downloading {0} to {1}...", entry.Url, entry.FileName);
                    try
                    {
                        File.WriteAllBytes(filePath, client.DownloadData(entry.Url));

                        try
                        {
                            File.Copy(filePath, filePathDev);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DASSET] Could not download asset.");
                        assetsDir = null;
                        return false;
                    }
                }
            }

            if (isRefreshNeeded)
                SetLocalAssetVer(baseDir, info.Version);

            assetsDir = newAssetsDir;

            Log.Verbose("[DASSET] Assets OK at {0}", assetsDir.FullName);

            CleanUpOld(baseDir, info.Version - 1);

            return true;
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

            try
            {
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

                var remoteVer = JsonConvert.DeserializeObject<AssetInfo>(client.DownloadString(ASSET_STORE_URL + "asset.json"));

                Log.Verbose("[DASSET] Ver check - local:{0} remote:{1}", localVer, remoteVer.Version);

                var needsUpdate = remoteVer.Version > localVer;

                return (needsUpdate, remoteVer);
            }
            catch (Exception e)
            {
                Log.Error(e, "[DASSET] Could not check asset version");
                return (false, null);
            }
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
            if (Util.CheckIsGameOpen())
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