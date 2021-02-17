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
            }
        }

        public static bool EnsureAssets(DirectoryInfo baseDir)
        {
            using var client = new WebClient();

            Log.Verbose("[DASSET] Starting asset download");

            var (isRefreshNeeded, info) = CheckAssetRefreshNeeded(baseDir);

            if (info == null)
                return false;

            foreach (var entry in info.Assets)
            {
                var filePath = Path.Combine(baseDir.FullName, entry.FileName);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                if (!File.Exists(filePath) || isRefreshNeeded)
                {
                    Log.Verbose("[DASSET] Downloading {0} to {1}...", entry.Url, entry.FileName);
                    try
                    {
                        File.WriteAllBytes(filePath, client.DownloadData(entry.Url));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[DASSET] Could not download asset.");
                        return false;
                    }
                }
            }

            if (isRefreshNeeded)
                SetLocalAssetVer(baseDir, info.Version);

            Log.Verbose("[DASSET] Assets OK");

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

                if (File.Exists(localVerFile))
                    localVer = int.Parse(File.ReadAllText(localVerFile));

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
    }
}
