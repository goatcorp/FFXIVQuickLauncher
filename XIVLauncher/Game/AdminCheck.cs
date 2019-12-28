using System.Collections.Generic;
using System.Security.Principal;
using System.Windows;
using Microsoft.Win32;

namespace XIVLauncher.Game
{
    static class AdminCheck
    {
        public static void RunCheck()
        {
            var runningAsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            var compatFlagKey = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", true);

            if (compatFlagKey != null)
            {
                var compatEntries = compatFlagKey.GetValueNames();

                var entriesToFix = new Stack<string>();
                foreach (var compatEntry in compatEntries)
                {
                    if ((compatEntry.Contains("ffxiv") || compatEntry.Contains("XIVLauncher")) && ((string) compatFlagKey.GetValue(compatEntry, string.Empty)).Contains("RUNASADMIN"))
                        entriesToFix.Push(compatEntry);
                }

                if (entriesToFix.Count > 0)
                {
                    var result = MessageBox.Show("XIVLauncher and/or FINAL FANTASY XIV are set to run as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nDo you want to fix this issue automatically?", "XIVLauncher Problem", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);

                    if (result != MessageBoxResult.OK) 
                        return;

                    while (entriesToFix.Count > 0)
                    {
                        compatFlagKey.DeleteValue(entriesToFix.Pop());
                    }

                    return;
                }
            }

            if (runningAsAdmin && !Properties.Settings.Default.HasComplainedAboutAdmin)
            {
                MessageBox.Show("XIVLauncher is running as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nPlease take care to avoid running XIVLauncher as admin.", "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Properties.Settings.Default.HasComplainedAboutAdmin = true;
                Properties.Settings.Default.Save();
            }
        }
    }
}
