using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace XIVLauncher.Common.Util
{
    public class ZipMethods
    {

        public static void AddIfExist(string entryPath, ZipArchive zip)
        {
            if (File.Exists(entryPath))
            {
                using var stream = File.Open(entryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var entry = zip.CreateEntry(new FileInfo(entryPath).Name);
                using var entryStream = entry.Open();
                stream.CopyTo(entryStream);
                //zip.CreateEntryFromFile(file.FullName, file.Name);
            }
            // directory handling solution based on answer from https://stackoverflow.com/a/62797701
            else if (Directory.Exists(entryPath))
            {
                var dir = new DirectoryInfo(entryPath);
                var folders = new Stack<string>();
                folders.Push(entryPath);

                do
                {
                    var currentFolder = folders.Pop();
                    foreach (var filename in Directory.GetFiles(currentFolder))
                    {
                        using var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        var entry = zip.CreateEntry($"{dir.Name}\\{filename.Substring(entryPath.Length + 1)}");
                        using var entryStream = entry.Open();
                        stream.CopyTo(entryStream);
                        //zip.CreateEntryFromFile(filename, $"{dir.Name}\\{filename.Substring(entryPath.Length + 1)}");
                    }
                    foreach (var dirname in Directory.GetDirectories(currentFolder))
                    {
                        folders.Push(dirname);
                    }
                } while (folders.Count > 0);
            }
        }
    }
}
