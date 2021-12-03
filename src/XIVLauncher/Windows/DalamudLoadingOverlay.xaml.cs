using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using CheapLoc;
using Serilog;
using XIVLauncher.Http;
using XIVLauncher.Windows.ViewModel;
using XIVLauncher.Xaml;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for DalamudLoadingOverlay.xaml
    /// </summary>
    public partial class DalamudLoadingOverlay : Window
    {
        public DalamudLoadingOverlay()
        {
            InitializeComponent();

            this.DataContext = new DalamudLoadingOverlayViewModel();
        }

        public enum DalamudLoadingProgress
        {
            Dalamud,
            Assets,
            Runtime,
            Unavailable
        }

        private DalamudLoadingProgress _progress;

        public void SetProgress(DalamudLoadingProgress progress)
        {
            _progress = progress;

            switch (progress)
            {
                case DalamudLoadingProgress.Dalamud:
                    ProgressTextBlock.Text = Loc.Localize("DalamudUpdateDalamud", "Updating Dalamud...");
                    break;
                case DalamudLoadingProgress.Assets:
                    ProgressTextBlock.Text = Loc.Localize("DalamudUpdateAssets", "Updating assets...");
                    break;
                case DalamudLoadingProgress.Runtime:
                    ProgressTextBlock.Text = Loc.Localize("DalamudUpdateRuntime", "Updating runtime...");
                    break;
                case DalamudLoadingProgress.Unavailable:
                    ProgressTextBlock.Text = Loc.Localize("DalamudUnavailable",
                        "Plugins are currently unavailable\ndue to a game update.");
                    InfoIcon.Visibility = Visibility.Visible;
                    ProgressBar.Visibility = Visibility.Collapsed;
                    UpdateText.Visibility = Visibility.Collapsed;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(progress), progress, null);
            }
        }

        public void SetVisible()
        {
            if (IsClosed)
                return;

            if (_progress == DalamudLoadingProgress.Unavailable)
            {
                var t = new Timer(15000) {AutoReset = false};

                t.Elapsed += (_, _) =>
                {
                    this.Dispatcher.Invoke(this.Close);
                };
                t.Start();
            }

            this.Show();
        }

        private void DalamudLoadingOverlay_OnLoaded(object sender, RoutedEventArgs e)
        {
            HideFromWindowSwitcher.Hide(this);
        }

        public bool IsClosed { get; private set; }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            IsClosed = true;
        }
    }
}
