using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Threading.Tasks;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Support
{
    public class DalamudAndPluginConfigImportExport : ZipMethods
    {
        public static async Task<string> ExportConfig(string storagePath)
        {
            // grab any platform-specific details
            // there's none to grab currently :)

            // start making our zip
            var outFile = new FileInfo(Path.Combine(storagePath, $"XIVLauncher-{DateTime.Now:yyyyMMddhhmmss}.xlconf"));
            using var archive = ZipFile.Open(outFile.FullName, ZipArchiveMode.Create);

            // add all the stock XIVLauncher based paths
            var accountsListFile = Path.Combine(storagePath, "accountsList.json");
            var dalamudConfigFile = Path.Combine(storagePath, "dalamudConfig.json");
            var dalamudVfsFile = Path.Combine(storagePath, "dalamudVfs.db");
            var pluginConfigsFolder = Path.Combine(storagePath, "pluginConfigs");

            ZipMethods.AddIfExist(accountsListFile, archive);
            ZipMethods.AddIfExist(dalamudConfigFile, archive);
            ZipMethods.AddIfExist(dalamudVfsFile, archive);
            ZipMethods.AddIfExist(pluginConfigsFolder, archive);

            // add some known special exceptions. It might be better to not build these expectations though
            var backupsFolder = Path.Combine(storagePath, "backups"); // Otter plugins
            var playerTrackBackupsFolder = Path.Combine(storagePath, "playerTrackBackups"); // PlayerTrack

            ZipMethods.AddIfExist(backupsFolder, archive);
            ZipMethods.AddIfExist(playerTrackBackupsFolder, archive);

            // return the folder containing our exported settings
            return outFile.FullName;
        }

        public static void ImportConfig(string zipFilePath, string storagePath)
        {
            // grab any platform-specific details
            // there's none to grab currently :)

            // TODO: Decide if we're going to alert on overwriting config.
            // Right now, Franz decided against it. The user has to intentionally try to use this feature
            // Also, .Net Framework is dumb and will explode if we use ZipArchive.ExtractToDirectory()
            // and there are any file conflicts. .Net Core doesn't have this issue though and provides
            // an override we could have used to do it anyways. Alternatively, we could just delete
            // all of the files/folders we'd be restoring to first, but that also feels bad.



            var inFile = new FileInfo(zipFilePath);
            using var archive = ZipFile.Open(inFile.FullName, ZipArchiveMode.Read);

            // If we weren't on .Net Framework, we could use this...
            // ZipFileExtensions.ExtractToDirectory(archive, storagePath, true);

            foreach (var entry in archive.Entries)
            {
                //var extractPath = storagePath + "\\" + entry.FullName;
                var extractPath = Path.Combine(storagePath, entry.FullName);


                // If we were going to warn about overwriting files, it would go here.
                /*
                bool promptAlwaysForOverwrite = true;
                if (promptAlwaysForOverwrite && File.Exists(extractPath))
                {
                    // Make some prompt. Overwrite? Yes, Yes to All, Cancel

                    if (result == no) break or something.
                    if (result == yestoall) promptAlwaysForOverwrite = false;
                    // yes is the default and needs no special handling.
                }
                */
                if (!Directory.Exists(Path.GetDirectoryName(extractPath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(extractPath));
                }
                ZipFileExtensions.ExtractToFile(entry, extractPath, true);
            }
        }
    }
}
