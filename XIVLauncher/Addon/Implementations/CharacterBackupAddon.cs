using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace XIVLauncher.Addon.Implementations
{
    class CharacterBackupAddon : INotifyAddonAfterClose
    {
        string IAddon.Name => "Sync Character Settings";

        void IAddon.Setup(Process game)
        {
            // Ignored
        }

        void INotifyAddonAfterClose.GameClosed()
        {
            var myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var charaFolderPath = new DirectoryInfo(Path.Combine(myDocumentsPath, "My Games", "FINAL FANTASY XIV - A Realm Reborn"));

            var backupDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "charDataBackup"));
            backupDirectory.Create();

            ZipFile.CreateFromDirectory(charaFolderPath.FullName, Path.Combine(backupDirectory.FullName, $"{DateTimeOffset.Now.ToUnixTimeSeconds()}.zip"));

            var currentBackups = backupDirectory.GetFiles("*.zip");

            if (currentBackups.Length > 3)
            {
                var oldestBackup = currentBackups.OrderBy(file =>
                {
                    return File.GetLastWriteTime(file.FullName);
                }).First();

                Serilog.Log.Information("Deleting oldest character backup: {0}", oldestBackup.FullName);
                oldestBackup.Delete();
            }


        }
    }
}
