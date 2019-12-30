using System;
using System.Collections.Generic;
using System.Linq;
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
            using (var updateManager = await UpdateManager.GitHubUpdateManager(repoUrl:RepoUrl, applicationName: "XIVLauncher", prerelease: downloadPrerelease))
            {
                SquirrelAwareApp.HandleEvents(
                    onInitialInstall: v => updateManager.CreateShortcutForThisExe(),
                    onAppUpdate: v => updateManager.CreateShortcutForThisExe(),
                    onAppUninstall: v => updateManager.RemoveShortcutForThisExe());

                await updateManager.UpdateApp();
            }
        }
    }
}
