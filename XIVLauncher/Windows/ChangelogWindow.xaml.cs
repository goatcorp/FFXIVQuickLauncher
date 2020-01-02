using System;
using System.Diagnostics;
using System.Media;
using System.Net;
using System.Windows;
using System.Windows.Documents;
using Newtonsoft.Json;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ChangelogWindow : Window
    {
        public ChangelogWindow()
        {
            InitializeComponent();

            IntroTextBlock.Text += " " + Util.GetAssemblyVersion() + ".";

            try
            {
                // GitHub requires TLS 1.2, we need to hardcode this for Windows 7
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.132 Safari/537.36");

                    dynamic releaseInfo = JsonConvert.DeserializeObject(
                        client.DownloadString("https://api.github.com/repos/goaaats/FFXIVQuickLauncher/releases/latest"));

                    ExceptionTextBox.AppendText((string) releaseInfo.body);
                }

                ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault;
            }
            catch(Exception)
            {
                ExceptionTextBox.AppendText("Couldn't get release info.");
            }
            
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

        private void DiscordButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/3NMcUV5");
        }
    }
}
