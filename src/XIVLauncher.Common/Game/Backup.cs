using System;
using System.IO;
using System.IO.Compression;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Game;

public sealed class BackupFileException(string filePath, Exception inner)
    : IOException($"Error accessing file: {filePath}", inner)
{
    public string FilePath { get; } = filePath;
}

public static class Backup
{
    private static readonly string[] IncludedRoamingFilesAndFolders =
    [
        "pluginConfigs",
        "installedPlugins",
        "dalamudVfs.db",
        "dalamudConfig.json",
        "dalamudUI.ini",
        "launcherConfigV3.json",
        "accountsList.json"
    ];

    private static readonly string[] IncludedUserFilesAndFolders =
    [
        "FFXIV_CHR*",
        "*.cfg",
        "*.dat",
        "cfgcpy"
    ];

    public static string BackupExtension = ".xivlauncher_backup";

    public static void CreateBackup(DirectoryInfo roamingPath, DirectoryInfo? userPath, FileInfo targetFile)
    {
        // Ensure target directory exists
        PlatformHelpers.CreateDirectoryHierarchy(targetFile.Directory!);

        using var fs = new FileStream(targetFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        // Add roaming files and folders at root of the archive
        foreach (var name in IncludedRoamingFilesAndFolders)
        {
            var filePath = Path.Combine(roamingPath.FullName, name);

            if (File.Exists(filePath))
            {
                AddFileToArchive(archive, new FileInfo(filePath), Path.GetFileName(filePath));
            }
            else if (Directory.Exists(filePath))
            {
                AddDirectoryToArchive(archive, new DirectoryInfo(filePath), roamingPath.FullName, string.Empty);
            }
            // else: missing item - ignore
        }

        // Add user files and folders under 'user/' prefix if userPath provided
        if (userPath != null && userPath.Exists)
        {
            foreach (var pattern in IncludedUserFilesAndFolders)
            {
                // If pattern contains wildcard characters, treat as file search
                if (pattern.IndexOfAny(['*', '?']) >= 0)
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(userPath.FullName, pattern, SearchOption.AllDirectories))
                        {
                            var relative = Path.GetRelativePath(userPath.FullName, file).Replace(Path.DirectorySeparatorChar, '/');
                            var entryName = $"user/{relative}";
                            AddFileToArchive(archive, new FileInfo(file), entryName);
                        }
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        throw new BackupFileException(userPath.FullName, ex);
                    }
                }
                else
                {
                    // pattern is a plain folder name - include whole directory if exists
                    var dirPath = Path.Combine(userPath.FullName, pattern);

                    if (Directory.Exists(dirPath))
                    {
                        AddDirectoryToArchive(archive, new DirectoryInfo(dirPath), userPath.FullName, "user");
                    }
                }
            }
        }
    }

    public static bool BackupHasUserFiles(FileInfo targetFile)
    {
        if (!targetFile.Exists) return false;

        using var fs = new FileStream(targetFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("user/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void RestoreBackup(DirectoryInfo roamingPath, DirectoryInfo? userPath, FileInfo targetFile)
    {
        if (!targetFile.Exists)
            throw new FileNotFoundException("Backup file not found", targetFile.FullName);

        try
        {
            using var fs = new FileStream(targetFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                // Normalize entry name separators
                var entryName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);

                if (entryName.StartsWith("user" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    // user files
                    if (userPath == null)
                        continue; // skip user files if no destination provided

                    var relative = entryName.Substring(("user" + Path.DirectorySeparatorChar).Length);
                    var outFile = new FileInfo(Path.Combine(userPath.FullName, relative));
                    ExtractEntryToFile(entry, outFile);
                }
                else
                {
                    // roaming files go to roamingPath root
                    var outFile = new FileInfo(Path.Combine(roamingPath.FullName, entryName));
                    ExtractEntryToFile(entry, outFile);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupFileException(targetFile.FullName, ex);
        }
    }

    // Helper: add single file to archive with given entry name
    private static void AddFileToArchive(ZipArchive archive, FileInfo file, string entryName)
    {
        // Normalize entry name to use forward slashes
        var normalized = entryName.Replace(Path.DirectorySeparatorChar, '/');
        var entry = archive.CreateEntry(normalized, CompressionLevel.Optimal);

        // Clear attributes/permissions - ExternalAttributes controls these bits
        entry.ExternalAttributes = 0;

        // To avoid preserving timestamps/ownership, set a consistent LastWriteTime
        entry.LastWriteTime = DateTimeOffset.UtcNow;

        try
        {
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            fileStream.CopyTo(entryStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupFileException(file.FullName, ex);
        }
    }

    // Helper: add entire directory recursively. basePath is the root whose relative path we compute; prefix is a relative prefix inside the archive (e.g., "user" or empty)
    private static void AddDirectoryToArchive(ZipArchive archive, DirectoryInfo dir, string basePath, string prefix)
    {
        var dirFull = dir.FullName;

        try
        {
            foreach (var file in Directory.EnumerateFiles(dirFull, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(basePath, file).Replace(Path.DirectorySeparatorChar, '/');
                var entryName = string.IsNullOrEmpty(prefix) ? relative : $"{prefix}/{relative}";
                AddFileToArchive(archive, new FileInfo(file), entryName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupFileException(dirFull, ex);
        }
    }

    // Helper: extract entry to outPath (creates directories and overwrites existing files)
    private static void ExtractEntryToFile(ZipArchiveEntry entry, FileInfo outFile)
    {
        PlatformHelpers.CreateDirectoryHierarchy(outFile.Directory!);

        // If the entry represents a directory (ends with '/'), skip
        if (entry.FullName.EndsWith('/'))
        {
            return;
        }

        try
        {
            using var entryStream = entry.Open();
            using var outStream = new FileStream(outFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
            entryStream.CopyTo(outStream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new BackupFileException(outFile.FullName, ex);
        }
    }
}
