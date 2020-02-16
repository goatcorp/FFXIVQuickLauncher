using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using XIVLauncher.Settings;

namespace XIVLauncher.Addon.Implementations
{
    class CharacterBackupAddon : INotifyAddonAfterClose
    {
        string IAddon.Name => "Sync Character Settings";

        void IAddon.Setup(Process gameProcess, ILauncherSettingsV3 setting)
        {
            // Ignored
        }

        void INotifyAddonAfterClose.GameClosed()
        {
            var myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var charaDirectory = new DirectoryInfo(Path.Combine(myDocumentsPath, "My Games", "FINAL FANTASY XIV - A Realm Reborn"));

            var backupDirectory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "charDataBackup", DateTimeOffset.Now.ToUnixTimeSeconds().ToString()));
            backupDirectory.Create();

            var backupFiles = charaDirectory.GetFiles("*.DAT", SearchOption.AllDirectories);

            foreach (var backupFile in backupFiles)
            {
                var newPath = backupDirectory.FullName +
                    backupFile.FullName.Split(new[] {"FINAL FANTASY XIV - A Realm Reborn"},
                        StringSplitOptions.None)[1];

                Directory.CreateDirectory(newPath.Substring(0, newPath.LastIndexOf("\\", StringComparison.InvariantCulture)));

                backupFile.CopyTo(newPath);
            }

            var currentBackups = backupDirectory.Parent.GetDirectories();

            if (currentBackups.Length > 3)
            {
                var oldestBackup = currentBackups.OrderBy(directoryInfo => int.Parse(directoryInfo.Name)).First();

                Serilog.Log.Information("Deleting oldest character backup: {0}", oldestBackup.FullName);
                oldestBackup.Delete(true);
            }
        }
    }
}
