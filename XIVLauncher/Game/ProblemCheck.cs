using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using CheapLoc;
using Microsoft.Win32;

namespace XIVLauncher.Game
{
    static class ProblemCheck
    {
        public static void RunCheck()
        {
            var runningAsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            var compatFlagKey = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", true);

            if (compatFlagKey != null && !EnvironmentSettings.IsWine)
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
                    var result = MessageBox.Show(Loc.Localize("AdminCheck", "XIVLauncher and/or FINAL FANTASY XIV are set to run as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nDo you want to fix this issue automatically?"), "XIVLauncher", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation);

                    if (result != MessageBoxResult.OK) 
                        return;

                    while (entriesToFix.Count > 0)
                    {
                        compatFlagKey.DeleteValue(entriesToFix.Pop());
                    }

                    return;
                }
            }

            if (runningAsAdmin && !Properties.Settings.Default.HasComplainedAboutAdmin && !EnvironmentSettings.IsWine)
            {
                MessageBox.Show(Loc.Localize("AdminCheckNag", "XIVLauncher is running as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nPlease take care to avoid running XIVLauncher as admin."), "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Properties.Settings.Default.HasComplainedAboutAdmin = true;
                Properties.Settings.Default.Save();
            }

            var procModules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>();
            if (procModules.Any(x => x.ModuleName == "MacType.dll" || x.ModuleName == "MacType64.dll"))
            {
                MessageBox.Show(Loc.Localize("MacTypeNag", "MacType was detected on this PC.\nIt will cause problems with FFXIV; both the official launcher and XIVLauncher.\n\nPlease exclude XIVLauncher, ffxivboot, ffxivlauncher, ffxivupdater and ffxiv_dx11 from MacType."), "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(-1);
            }
        }
    }
}
