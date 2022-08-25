using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using XIVLauncher.Common;
using ZipArchive = System.IO.Compression.ZipArchive;

namespace XIVLauncher.Support
{
    public static class PackGenerator
    {
        public static string SavePack()
        {
            var outFile = new FileInfo(Path.Combine(Paths.RoamingPath, $"trouble-{DateTime.Now:yyyyMMddhhmmss}.tspack"));
            using var archive = ZipFile.Open(outFile.FullName, ZipArchiveMode.Create);

            var troubleBytes = Encoding.UTF8.GetBytes(Troubleshooting.GetTroubleshootingJson());
            var troubleEntry = archive.CreateEntry("trouble.json").Open();
            troubleEntry.Write(troubleBytes, 0, troubleBytes.Length);
            troubleEntry.Close();

            var xlLogFile = new FileInfo(Path.Combine(Paths.RoamingPath, "output.log"));
            var patcherLogFile = new FileInfo(Path.Combine(Paths.RoamingPath, "patcher.log"));
            var dalamudLogFile = new FileInfo(Path.Combine(Paths.RoamingPath, "dalamud.log"));
            var dalamudInjectorLogFile = new FileInfo(Path.Combine(Paths.RoamingPath, "dalamud.injector.log"));
            var dalamudBootLogFile = new FileInfo(Path.Combine(Paths.RoamingPath, "dalamud.boot.log"));  
            var ariaLogFile = new FileInfo(Path.Combine(Paths.RoamingPath, "aria.log"));

            AddIfExist(xlLogFile, archive);
            AddIfExist(patcherLogFile, archive);
            AddIfExist(dalamudLogFile, archive);
            AddIfExist(dalamudInjectorLogFile, archive);
            AddIfExist(dalamudBootLogFile, archive);
            AddIfExist(ariaLogFile, archive);

            return outFile.FullName;
        }

        private static void AddIfExist(FileInfo file, ZipArchive zip)
        {
            if (file.Exists)
            {
                using var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var entry = zip.CreateEntry(file.Name);
                using var entryStream = entry.Open();
                stream.CopyTo(entryStream);
                //zip.CreateEntryFromFile(file.FullName, file.Name);
            }
        }
    }
}