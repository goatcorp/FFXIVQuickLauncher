using System;
using System.Net;
using System.Net.Security;
using System.Windows.Forms;

namespace XIVLauncher
{
    public static class VersionChecker
    {
        private const string Repo = "goaaats/FFXIVQuickLauncher";

        public static void CheckVersion()
        {
#if DEBUG
            return;
#endif
            
            var currentHash = Util.GetGitHash();

            // If this is a working copy, don't alert about new versions
            if (currentHash.Contains("dirty"))
                return;

            var newCommit = GetNewestCommit();
            
            if (!newCommit.Sha.StartsWith(currentHash))
            {
                MessageBox.Show(
                    "There is a new version available. Please download it from the github repo.",
                    "New Version available", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                
                System.Diagnostics.Process.Start($"https://github.com/{Repo}/releases");
                Environment.Exit(0);
            }    
        }

        private static GitHubCommit GetNewestCommit()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "XIVMon");
                var result =
                    client.DownloadString($"https://api.github.com/repos/{Repo}/commits");
                
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
                return GitHubCommit.FromJson(result)[0];
            }
        }
    }

}