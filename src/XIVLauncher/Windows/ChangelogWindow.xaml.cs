using System;
using System.Diagnostics;
using System.Media;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Settings;
using XIVLauncher.Windows.ViewModel;
using HttpUtility = System.Web.HttpUtility;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ChangelogWindow : Window
    {
        private const string META_URL = "https://kamori.goats.dev/Proxy/Meta";

        public class VersionMeta
        {
            [JsonProperty("version")]
            public string Version { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("changelog")]
            public string Changelog { get; set; }

            [JsonProperty("when")]
            public DateTime When { get; set; }
        }

        public class ReleaseMeta
        {
            [JsonProperty("releaseVersion")]
            public VersionMeta ReleaseVersion { get; set; }

            [JsonProperty("prereleaseVersion")]
            public VersionMeta PrereleaseVersion { get; set; }
        }

        public ChangelogWindow(bool prerelease, string version)
        {
            InitializeComponent();

            DiscordButton.Click += Util.OpenDiscord;

            var vm = new ChangeLogWindowViewModel();
            DataContext = vm;

            UpdateNotice.Text = string.Format(vm.UpdateNoticeLoc, version);

            var _ = Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient();
                    var response = JsonConvert.DeserializeObject<ReleaseMeta>(await client.GetStringAsync(META_URL));

                    Dispatcher.Invoke(() => this.ChangeLogText.Text = prerelease ? response.PrereleaseVersion.Changelog : response.ReleaseVersion.Changelog);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not get changelog");
                    Dispatcher.Invoke(() => this.ChangeLogText.Text = vm.ChangelogLoadingErrorLoc);
                }
            });

            this.ChangeLogText.Text = vm.ChangelogLoadingLoc;

            SystemSounds.Asterisk.Play();

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void EmailButton_OnClick(object sender, RoutedEventArgs e)
        {
            // Try getting the Windows 10 "build", e.g. 1909
            var releaseId = "???";
            try
            {
                releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion",
                    "ReleaseId", "").ToString();
            }
            catch
            {
                // ignored
            }

            var os = HttpUtility.HtmlEncode($"{Environment.OSVersion} - {releaseId} ({Environment.Version})");
            var lang = HttpUtility.HtmlEncode(App.Settings.LauncherLanguage.GetValueOrDefault(LauncherLanguage.English)
                .ToString());
            var wine = EnvironmentSettings.IsWine ? "Yes" : "No";

            Process.Start(string.Format(
                "mailto:goatsdev@protonmail.com?subject=XIVLauncher%20Feedback&body=This%20is%20my%20XIVLauncher%20Feedback.%0A%0AMy%20OS%3A%0D{0}%0ALauncher%20Language%3A%0D{1}%0ARunning%20on%20Wine%3A%0D{2}",
                os, lang, wine));
        }
    }
}