using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using CheapLoc;
using Serilog;
using Squirrel;
using XIVLauncher.Windows;

#nullable enable

namespace XIVLauncher
{
    class Updates
    {
        public event Action<bool>? OnUpdateCheckFinished;

        public async Task Run(bool downloadPrerelease, ChangelogWindow changelogWindow)
        {
            // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var url = "https://kamori.goats.dev/Proxy/Update";
            if (downloadPrerelease)
                url += "/Prerelease";
            else
                url += "/Release";

            try
            {
                ReleaseEntry? newRelease = null;

                using (var updateManager = new UpdateManager(url, "XIVLauncher"))
                {
                    // TODO: is this allowed?
                    SquirrelAwareApp.HandleEvents(
                        onInitialInstall: (v, t) => updateManager.CreateShortcutForThisExe(),
                        onAppUpdate: (v, t) => updateManager.CreateShortcutForThisExe(),
                        onAppUninstall: (v, t) => updateManager.RemoveShortcutForThisExe());

#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task
                    var a = await updateManager.CheckForUpdate();
                    newRelease = await updateManager.UpdateApp();
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
                }

                if (newRelease != null)
                {
                    if (changelogWindow == null)
                    {
                        Log.Error("changelogWindow was null");
                        UpdateManager.RestartApp();
                        return;
                    }

                    try
                    {
                        changelogWindow.Dispatcher.Invoke(() =>
                        {
                            changelogWindow.UpdateVersion(newRelease.Version.ToString());
                            changelogWindow.Show();
                            changelogWindow.Closed += (_, _) =>
                            {
                                UpdateManager.RestartApp();
                            };
                        });

                        OnUpdateCheckFinished?.Invoke(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Could not show changelog window");
                        UpdateManager.RestartApp();
                    }
                }
#if !XL_NOAUTOUPDATE
                else
                    OnUpdateCheckFinished?.Invoke(true);
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Update failed");
                CustomMessageBox.Show(Loc.Localize("updatefailureerror", "XIVLauncher failed to check for updates. This may be caused by connectivity issues to GitHub. Wait a few minutes and try again.\nDisable your VPN, if you have one. You may also have to exclude XIVLauncher from your antivirus.\nIf this continues to fail after several minutes, please check out the FAQ."),
                                "XIVLauncher",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error, showOfficialLauncher: true);
                System.Environment.Exit(1);
            }

            // Reset security protocol after updating
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }
    }
}

#nullable disable