using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Squirrel;

namespace XIVLauncher
{
    static class Updates
    {
        private const string RepoUrl = "https://github.com/goaaats/FFXIVQuickLauncher";

        public static async Task Run(bool downloadPrerelease = false)
        {
            // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var updateManager = await UpdateManager.GitHubUpdateManager(repoUrl:RepoUrl, applicationName: "XIVLauncher", prerelease: downloadPrerelease))
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => updateManager.CreateShortcutForThisExe(),
                    onAppUpdate: v => updateManager.CreateShortcutForThisExe(),
                    onAppUninstall: v => updateManager.RemoveShortcutForThisExe());

                var downloadedRelease = await updateManager.UpdateApp();

                if (downloadedRelease != null)
                    UpdateManager.RestartApp();
            }

            // Reset security protocol after updating
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }
    }
}
