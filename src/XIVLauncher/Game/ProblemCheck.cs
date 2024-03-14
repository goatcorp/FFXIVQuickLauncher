using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using CheapLoc;
using Microsoft.Win32;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Util;
using XIVLauncher.Windows;

namespace XIVLauncher.Game
{
    static class ProblemCheck
    {
        private static string GetCmdPath() => Path.Combine(Environment.ExpandEnvironmentVariables("%WINDIR%"), "System32", "cmd.exe");

        public static void RunCheck(Window parentWindow)
        {
            if (EnvironmentSettings.IsWine)
                return;

            var compatFlagKey = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", true);

            if (compatFlagKey != null && !EnvironmentSettings.IsWine && !App.Settings.HasComplainedAboutAdmin.GetValueOrDefault(false))
            {
                var compatEntries = compatFlagKey.GetValueNames();

                var entriesToFix = new Stack<string>();

                foreach (var compatEntry in compatEntries)
                {
                    if ((compatEntry.Contains("ffxiv_dx11") || compatEntry.Contains("XIVLauncher")) && ((string)compatFlagKey.GetValue(compatEntry, string.Empty)).Contains("RUNASADMIN"))
                        entriesToFix.Push(compatEntry);
                }

                if (entriesToFix.Count > 0)
                {
                    var result = CustomMessageBox.Show(
                        Loc.Localize("AdminCheck",
                            "XIVLauncher and/or the game are set to run as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nDo you want to fix this issue automatically?"),
                        "XIVLauncher", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation, parentWindow: parentWindow);

                    if (result != MessageBoxResult.OK)
                        return;

                    while (entriesToFix.Count > 0)
                    {
                        compatFlagKey.DeleteValue(entriesToFix.Pop());
                    }

                    return;
                }

                App.Settings.HasComplainedAboutAdmin = true;
            }

            if (PlatformHelpers.IsElevated() && !App.Settings.HasComplainedAboutAdmin.GetValueOrDefault(false) && !EnvironmentSettings.IsWine)
            {
                CustomMessageBox.Show(
                    Loc.Localize("AdminCheckNag",
                        "XIVLauncher is running as administrator.\nThis can cause various issues, including addons failing to launch and hotkey applications failing to respond.\n\nPlease take care to avoid running XIVLauncher as admin."),
                    "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation, parentWindow: parentWindow);
                App.Settings.HasComplainedAboutAdmin = true;
            }

            var procModules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>();

            if (procModules.Any(x => x.ModuleName == "MacType.dll" || x.ModuleName == "MacType64.dll"))
            {
                CustomMessageBox.Show(
                    Loc.Localize("MacTypeNag",
                        "MacType was detected on this PC.\nIt will cause problems with the game; both on the official launcher and XIVLauncher.\n\nPlease exclude XIVLauncher, ffxivboot, ffxivlauncher, ffxivupdater and ffxiv_dx11 from MacType."),
                    "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Error, parentWindow: parentWindow);
                Environment.Exit(-1);
            }

            if (!CheckMyGamesWriteAccess())
            {
                CustomMessageBox.Show(
                    Loc.Localize("MyGamesWriteAccessNag",
                        "You do not have permission to write to the game's My Games folder.\nThis will prevent screenshots and some character data from being saved.\n\nThis may be caused by either your antivirus or a permissions error. Please check your My Games folder permissions."),
                    "XIVLauncher Problem", MessageBoxButton.OK, MessageBoxImage.Exclamation, parentWindow: parentWindow);
            }

            if (App.Settings.GamePath == null)
                return;

            var gameFolderPath = Path.Combine(App.Settings.GamePath.FullName, "game");

            var d3d11 = new FileInfo(Path.Combine(gameFolderPath, "d3d11.dll"));
            var dxgi = new FileInfo(Path.Combine(gameFolderPath, "dxgi.dll"));
            var dinput8 = new FileInfo(Path.Combine(gameFolderPath, "dinput8.dll"));

            if (!CheckSymlinkValid(d3d11) || !CheckSymlinkValid(dxgi) || !CheckSymlinkValid(dinput8))
            {
                if (CustomMessageBox.Builder
                                    .NewFrom(Loc.Localize("GShadeSymlinks",
                                        "GShade symbolic links are corrupted.\n\nThe game cannot start. Do you want XIVLauncher to fix this? You will need to reinstall GShade."))
                                    .WithButtons(MessageBoxButton.YesNo)
                                    .WithImage(MessageBoxImage.Error)
                                    .WithParentWindow(parentWindow)
                                    .Show() == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (d3d11.Exists)
                            ElevatedDelete(d3d11);

                        if (dxgi.Exists)
                            ElevatedDelete(dxgi);

                        if (dinput8.Exists)
                            ElevatedDelete(dinput8);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not delete broken GShade symlinks");
                    }
                }
            }

            d3d11.Refresh();
            dinput8.Refresh();
            dxgi.Refresh();

            if (d3d11.Exists && dxgi.Exists)
            {
                var dxgiInfo = FileVersionInfo.GetVersionInfo(dxgi.FullName);
                var d3d11Info = FileVersionInfo.GetVersionInfo(d3d11.FullName);

                if (dxgiInfo.ProductName?.Equals("GShade", StringComparison.OrdinalIgnoreCase) == true &&
                    d3d11Info.ProductName?.Equals("GShade", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (CustomMessageBox.Builder
                                        .NewFrom(Loc.Localize("GShadeError",
                                            "A broken GShade installation was detected.\n\nThe game cannot start. Do you want XIVLauncher to fix this? You will need to reinstall GShade."))
                                        .WithButtons(MessageBoxButton.YesNo)
                                        .WithImage(MessageBoxImage.Error)
                                        .WithParentWindow(parentWindow)
                                        .Show() == MessageBoxResult.Yes)
                    {
                        try
                        {
                            ElevatedDelete(d3d11, dxgi);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Could not delete duplicate GShade");
                        }
                    }
                }
            }

            d3d11.Refresh();
            dinput8.Refresh();
            dxgi.Refresh();

            if ((d3d11.Exists || dinput8.Exists) && !App.Settings.HasComplainedAboutGShadeDxgi.GetValueOrDefault(false))
            {
                FileVersionInfo d3d11Info = null;
                FileVersionInfo dinput8Info = null;

                if (d3d11.Exists)
                    d3d11Info = FileVersionInfo.GetVersionInfo(d3d11.FullName);

                if (dinput8.Exists)
                    dinput8Info = FileVersionInfo.GetVersionInfo(dinput8.FullName);

                if ((d3d11Info?.ProductName?.Equals("GShade", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (dinput8Info?.ProductName?.Equals("GShade", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    if (CustomMessageBox.Builder
                                        .NewFrom(Loc.Localize("GShadeWrongMode",
                                            "You installed GShade in a mode that isn't optimal for use together with XIVLauncher. Do you want XIVLauncher to fix this for you?\n\nThis will not change your presets or settings, it will merely improve compatibility with XIVLauncher features."))
                                        .WithButtons(MessageBoxButton.YesNo)
                                        .WithImage(MessageBoxImage.Warning)
                                        .WithParentWindow(parentWindow)
                                        .Show() == MessageBoxResult.Yes)
                    {
                        try
                        {
                            var toMove = d3d11.Exists ? d3d11 : dinput8;

                            var psi = new ProcessStartInfo
                            {
                                Verb = "runas",
                                FileName = GetCmdPath(),
                                WorkingDirectory = Paths.ResourcesPath,
                                Arguments = $"/C \"move \"{Path.Combine(gameFolderPath, toMove.Name)}\" \"{Path.Combine(gameFolderPath, "dxgi.dll")}\"\"",
                                UseShellExecute = true,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };

                            var process = Process.Start(psi);

                            if (process == null)
                            {
                                throw new Exception("Could not spawn CMD when fixing GShade");
                            }

                            process.WaitForExit();

                            var gshadeInstKey = Registry.LocalMachine.OpenSubKey(
                                "SOFTWARE\\GShade\\Installations", false);

                            if (gshadeInstKey != null)
                            {
                                var gshadeInstSubKeys = gshadeInstKey.GetSubKeyNames();

                                var gshadeInstsToFix = new Stack<string>();

                                foreach (var gshadeInst in gshadeInstSubKeys)
                                {
                                    if (gshadeInst.Contains("ffxiv_dx11.exe"))
                                    {
                                        gshadeInstsToFix.Push(gshadeInst);
                                    }
                                }

                                if (gshadeInstsToFix.Count > 0)
                                {
                                    while (gshadeInstsToFix.Count > 0)
                                    {
                                        var gshadePsi = new ProcessStartInfo
                                        {
                                            Verb = "runas",
                                            FileName = "reg.exe",
                                            WorkingDirectory = Environment.SystemDirectory,
                                            Arguments = $"add \"HKLM\\SOFTWARE\\GShade\\Installations\\{gshadeInstsToFix.Pop()}\" /v \"altdxmode\" /t \"REG_SZ\" /d \"0\" /f",
                                            UseShellExecute = true,
                                            CreateNoWindow = true,
                                            WindowStyle = ProcessWindowStyle.Hidden
                                        };

                                        var gshadeProcess = Process.Start(gshadePsi);

                                        if (gshadeProcess == null)
                                        {
                                            throw new Exception("Could not spawn reg when fixing GShade");
                                        }

                                        gshadeProcess.WaitForExit();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Could not fix GShade incompatibility");
                        }
                    }
                    else
                    {
                        App.Settings.HasComplainedAboutGShadeDxgi = true;
                    }
                }
            }
        }

        private static void ElevatedDelete(params FileInfo[] info)
        {
            var pathsToDelete = info.Select(x => $"\"{x.FullName}\"").Aggregate("", (current, name) => current + $"{name} ");

            var psi = new ProcessStartInfo
            {
                Verb = "runas",
                FileName = GetCmdPath(),
                Arguments = $"/C \"del {pathsToDelete}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var process = Process.Start(psi);

            if (process == null)
            {
                throw new Exception("Could not spawn CMD for elevated delete");
            }

            process.WaitForExit();
        }

        private static bool CheckMyGamesWriteAccess()
        {
            // Create a randomly-named file in the game's user data folder and make sure we don't
            // get a permissions error.
            var myGames = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "my games");
            if (!Directory.Exists(myGames))
                return true;

            var targetPath = Directory.GetDirectories(myGames).FirstOrDefault(x => Path.GetDirectoryName(x)?.Length == 34);
            if (targetPath == null)
                return true;

            var tempFile = Path.Combine(targetPath, Guid.NewGuid().ToString());

            try
            {
                var file = File.Create(tempFile);
                file.Dispose();
                File.Delete(tempFile);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception)
            {
                return true;
            }

            return true;
        }

        private static bool CheckSymlinkValid(FileInfo file)
        {
            if (!file.Exists)
                return true;

            try
            {
                file.OpenRead();
            }
            catch (IOException)
            {
                return false;
            }

            return true;
        }
    }
}
