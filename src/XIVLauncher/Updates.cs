using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        public event Action<bool> OnUpdateCheckFinished;

        private class ErrorNewsData
        {
            [JsonPropertyName("until")]
            public uint ShowUntil { get; set; }

            [JsonPropertyName("message")]
            public string Message { get; set; }

            [JsonPropertyName("isError")]
            public bool IsError { get; set; }
        }

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
                ReleaseEntry newRelease = null;

                using (var updateManager = new UpdateManager(url, "XIVLauncher"))
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
                ErrorNewsData? newsData = null;

                try
                {
                    const string NEWS_URL = "https://gist.githubusercontent.com/goaaats/5968072474f79b066a60854d38b95280/raw/xl-news.txt";

                    using var client = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(10),
                    };

                    var text = await client.GetStringAsync(NEWS_URL).ConfigureAwait(false);
                    newsData = JsonSerializer.Deserialize<ErrorNewsData>(text);
                }
                catch (Exception newsEx)
                {
                    Log.Error(newsEx, "Could not get error news");
                }

                if (newsData != null && !string.IsNullOrEmpty(newsData.Message) && DateTimeOffset.UtcNow.ToUnixTimeSeconds() < newsData.ShowUntil)
                {
                    CustomMessageBox.Show(newsData.Message,
                        "XIVLauncher",
                        MessageBoxButton.OK,
                        newsData.IsError ? MessageBoxImage.Error : MessageBoxImage.Asterisk, showOfficialLauncher: true);
                }
                else
                {
                    CustomMessageBox.Show(Loc.Localize("updatefailureerror", "XIVLauncher failed to check for updates. This may be caused by internet connectivity issues. Wait a few minutes and try again.\nDisable your VPN, if you have one. You may also have to exclude XIVLauncher from your antivirus.\nIf this continues to fail after several minutes, please check out the FAQ."),
                        "XIVLauncher",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error, showOfficialLauncher: true);
                }

                System.Environment.Exit(1);
            }

            // Reset security protocol after updating
            ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
        }
    }
}