using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections.Generic;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common.Http.HappyEyeballs;
using XIVLauncher.Support;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ChangelogWindow : Window
    {
        private readonly bool _prerelease;
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

        private ChangeLogWindowViewModel Model => this.DataContext as ChangeLogWindowViewModel;

        public ChangelogWindow(bool prerelease)
        {
            _prerelease = prerelease;
            InitializeComponent();

            DiscordButton.Click += SupportLinks.OpenDiscord;

            var vm = new ChangeLogWindowViewModel();
            DataContext = vm;

            ChangeLogViewer.Document = BuildFlowDocumentFromPlainText(vm.ChangelogLoadingLoc);

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        public void UpdateVersion(string version)
        {
            UpdateNotice.Text = string.Format(Model.UpdateNoticeLoc, version);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public new void Show()
        {
            LoadChangelog();

            SystemSounds.Asterisk.Play();
            base.Show();
        }

        public new void ShowDialog()
        {
            LoadChangelog();

            base.ShowDialog();
        }

        private void LoadChangelog()
        {
            var _ = Task.Run(this.FetchChangelogAsync);
        }

        private async Task FetchChangelogAsync()
        {
            try
            {
                var client = HappyHttpClient.SharedClient;
                var response = JsonConvert.DeserializeObject<ReleaseMeta>(await client.GetStringAsync(META_URL));

                var text = _prerelease ? response.PrereleaseVersion?.Changelog : response.ReleaseVersion?.Changelog;
                Dispatcher.Invoke(() => ChangeLogViewer.Document = BuildFlowDocumentFromPlainText(text));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not get changelog");
                Dispatcher.Invoke(() => ChangeLogViewer.Document = BuildFlowDocumentFromPlainText(Model.ChangelogLoadingErrorLoc));
            }
        }

        private FlowDocument BuildFlowDocumentFromPlainText(string text)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("pack://application:,,,/MaterialDesignThemes.Wpf;component/Resources/Roboto/#Roboto"),
                FontSize = 12,
                PagePadding = new Thickness(0),
                ColumnWidth = double.PositiveInfinity // don't flow into newspaper columns
            };

            if (string.IsNullOrEmpty(text))
                return doc;

            // Normalize line endings
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = normalized.Split('\n');

            List currentList = null;
            var paraLines = new List<string>();

            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    // Empty line -> flush current list or paragraph
                    if (currentList != null)
                    {
                        doc.Blocks.Add(currentList);
                        currentList = null;
                    }

                    if (paraLines.Count > 0)
                    {
                        var p = new Paragraph(new Run(string.Join(" ", paraLines)));
                        p.Margin = new Thickness(0, 0, 0, 8);
                        doc.Blocks.Add(p);
                        paraLines.Clear();
                    }

                    continue;
                }

                var trimmedStart = raw.TrimStart();
                if (trimmedStart.StartsWith('*'))
                {
                    // Flush paragraph buffer
                    if (paraLines.Count > 0)
                    {
                        var p = new Paragraph(new Run(string.Join(" ", paraLines)));
                        p.Margin = new Thickness(0, 0, 0, 8);
                        doc.Blocks.Add(p);
                        paraLines.Clear();
                    }

                    // Start a new list if needed
                    currentList ??= new List
                    {
                        MarkerStyle = TextMarkerStyle.Disc,
                        Margin = new Thickness(6, 0, 0, 8),
                        MarkerOffset = 15
                    };

                    var itemText = trimmedStart.TrimStart('*').Trim();
                    var itemPara = new Paragraph(new Run(itemText)) { Margin = new Thickness(0, 0, 0, 4) };
                    currentList.ListItems.Add(new ListItem(itemPara));
                }
                else
                {
                    // Normal text line, accumulate into paragraph
                    paraLines.Add(raw.Trim());
                }
            }

            // Flush remaining buffers
            if (currentList != null)
                doc.Blocks.Add(currentList);

            if (paraLines.Count > 0)
            {
                var p = new Paragraph(new Run(string.Join(" ", paraLines)));
                p.Margin = new Thickness(0, 0, 0, 8);
                doc.Blocks.Add(p);
            }

            return doc;
        }
    }
}


