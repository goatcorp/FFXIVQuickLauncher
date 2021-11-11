using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CheapLoc;
using Serilog;
using Squirrel;
using XIVLauncher.Windows;

namespace XIVLauncher
{
    class Updates
    {
#if !XL_NOAUTOUPDATE
        public EventHandler OnUpdateCheckFinished;
#endif

        public async Task Run(bool downloadPrerelease = false)
        {
            // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            try
            {
                ReleaseEntry newRelease = null;

                using (var updateManager = await UpdateManager.GitHubUpdateManager(repoUrl: App.RepoUrl, applicationName: "XIVLauncher", prerelease: downloadPrerelease))
                {
                    // TODO: is this allowed?
                    SquirrelAwareApp.HandleEvents(
                        onInitialInstall: v => updateManager.CreateShortcutForThisExe(),
                        onAppUpdate: v => updateManager.CreateShortcutForThisExe(),
                        onAppUninstall: v => updateManager.RemoveShortcutForThisExe());

                    var a = await updateManager.CheckForUpdate();
                    newRelease = await updateManager.UpdateApp();
                }

                if (newRelease != null)
                {
                    /*
                    MessageBox.Show(Loc.Localize("UpdateNotice", "An update for XIVLauncher is available and will now be installed."),
                        "XIVLauncher Update", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                    */
                    UpdateManager.RestartApp();
                }
#if !XL_NOAUTOUPDATE
                else
                    OnUpdateCheckFinished?.Invoke(this, null);
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Update failed");
                CustomMessageBox.Show(Loc.Localize("updatefailureerror", "XIVLauncher failed to check for updates. This may be caused by connectivity issues to GitHub. Wait a few minutes and try again.\nDisable your VPN, if you have one. You may also have to exclude XIVLauncher from your antivirus.\nIf this continues to fail after several minutes, please check out the FAQ."),
                                "XIVLauncher",
                                 MessageBoxButton.OK,
                                 MessageBoxImage.Error);
                System.Environment.Exit(1);
            }



            // Reset security protocol after updating
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }
    }
}