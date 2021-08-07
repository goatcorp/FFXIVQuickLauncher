using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Web;
using System.Windows;
using Microsoft.Win32;
using XIVLauncher.Settings;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ChangelogWindow
    {
        public ChangelogWindow()
        {
            InitializeComponent();

            DiscordButton.Click += Util.OpenDiscord;
            DataContext = new ChangeLogWindowViewModel();

            this.ChangeLogText.Text = File.ReadAllText(Path.Combine(Paths.ResourcesPath, "CHANGELOG.txt"));
            
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
