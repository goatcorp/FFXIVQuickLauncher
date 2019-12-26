using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Xceed.Wpf.Toolkit.PropertyGrid.Converters;

namespace XIVLauncher.Game
{
    static class AdminCheck
    {
        public static void RunCheck()
        {
            var compatFlagKey = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", true);

            var compatEntries = compatFlagKey.GetValueNames();

            var entriesToFix = new Stack<string>();
            foreach (var compatEntry in compatEntries)
            {
                if ((compatEntry.Contains("ffxiv") || compatEntry.Contains("XIVLauncher")) && ((string) compatFlagKey.GetValue(compatEntry, string.Empty)).Contains("RUNASADMIN"))
                    entriesToFix.Push(compatEntry);
            }

            var runningAsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

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

            if (runningAsAdmin)
            {
                MessageBox.Show("XIVLauncher is running as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nPlease take care to avoid running XIVLauncher as admin.", "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
        }
    }
}
