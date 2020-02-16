using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using XIVLauncher.Settings;

namespace XIVLauncher.Addon.Implementations
{
    class CharacterSyncAddon : INotifyAddonAfterClose
    {
        string IAddon.Name => "Sync Character Settings";

        void IAddon.Setup(Process gameProcess, ILauncherSettingsV3 setting)
        {
            // Ignored
        }

        void INotifyAddonAfterClose.GameClosed()
        {
            var myDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var charaFolderPath = new DirectoryInfo(Path.Combine(myDocumentsPath, "My Games", "FINAL FANTASY XIV - A Realm Reborn"));

            var orderedByChanges = charaFolderPath.GetDirectories("FFXIV_CHR*").OrderByDescending(folder =>
            {
                return File.GetLastWriteTime(Path.Combine(folder.FullName, "ADDON.DAT"));
            });

            var lastChanged = orderedByChanges.First();
            var toCopyTo = orderedByChanges.Skip(1);

            Serilog.Log.Information("Found {0} character folders, most up to date one is {1}, syncing now...", orderedByChanges.Count(), lastChanged.Name);

            string[] copyable = { "ADDON.DAT", "COMMON.DAT", "CONTROL0.DAT", "CONTROL1.DAT", "HOTBAR.DAT", "KEYBIND.DAT", "LOGFLTR.DAT", "MACRO.DAT" };
            var files = lastChanged.GetFiles("*.DAT")
                .Where(s => copyable.Any(c => c == s.Name));

            foreach (var folder in toCopyTo)
            {
                if (!folder.Name.StartsWith("FFXIV_CHR"))
                    continue;

                Serilog.Log.Information("Copying to {0}", folder.Name);

                foreach (var file in files)
                {
                    var destPath = Path.Combine(folder.FullName, file.Name);
                    File.Copy(file.FullName, destPath, true);
                    Serilog.Log.Information("   -> Copied {0} to {1}", file.FullName, destPath);
                }
            }
        }
    }
}
