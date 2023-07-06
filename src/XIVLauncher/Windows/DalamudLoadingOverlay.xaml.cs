using System;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CheapLoc;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Util;
using XIVLauncher.Windows.ViewModel;
using XIVLauncher.Xaml;

namespace XIVLauncher.Windows
{
    // TODO(goat): Dispatcher!!

    /// <summary>
    /// Interaction logic for DalamudLoadingOverlay.xaml
    /// </summary>
    public partial class DalamudLoadingOverlay : Window, IDalamudLoadingOverlay
    {
        public DalamudLoadingOverlay()
        {
            InitializeComponent();

            this.DataContext = new DalamudLoadingOverlayViewModel();

            var interop = new WindowInteropHelper(this);
            interop.EnsureHandle();
        }

        private IDalamudLoadingOverlay.DalamudUpdateStep _progress;

        public void SetStep(IDalamudLoadingOverlay.DalamudUpdateStep progress)
        {
            Dispatcher.Invoke(() =>
            {
                _progress = progress;

                switch (progress)
                {
                    case IDalamudLoadingOverlay.DalamudUpdateStep.Dalamud:
                        ProgressTextBlock.Text = Loc.Localize("DalamudUpdateDalamud", "Updating core...");
                        break;

                    case IDalamudLoadingOverlay.DalamudUpdateStep.Assets:
                        ProgressTextBlock.Text = Loc.Localize("DalamudUpdateAssets", "Updating assets...");
                        break;

                    case IDalamudLoadingOverlay.DalamudUpdateStep.Runtime:
                        ProgressTextBlock.Text = Loc.Localize("DalamudUpdateRuntime", "Updating runtime...");
                        break;

                    case IDalamudLoadingOverlay.DalamudUpdateStep.Starting:
                        ProgressTextBlock.Text = Loc.Localize("DalamudNowStarting", "Starting...");
                        break;

                    case IDalamudLoadingOverlay.DalamudUpdateStep.Unavailable:
                        ProgressTextBlock.Text = Loc.Localize("DalamudUnavailable",
                            "Plugins are currently unavailable\ndue to a game update.");
                        InfoIcon.Visibility = Visibility.Visible;
                        ProgressBar.Visibility = Visibility.Collapsed;
                        UpdateText.Visibility = Visibility.Collapsed;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(progress), progress, null);
                }
            });
        }

        public void SetVisible()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (IsClosed)
                    return;

                // TODO(goat): this is real bad, just do it any other way that doesn't possibly block
                if (_progress == IDalamudLoadingOverlay.DalamudUpdateStep.Unavailable)
                {
                    var t = new Timer(15000) {AutoReset = false};

                    t.Elapsed += (_, _) =>
                    {
                        this.Dispatcher.Invoke(this.Close);
                    };
                    t.Start();
                }

                this.Show();
            });
        }

        public void SetInvisible()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (IsClosed)
                    return;

                this.Hide();
            });
        }

        public void ReportProgress(long? size, long downloaded, double? progress)
        {
            Dispatcher.Invoke(() =>
            {
                if (IsClosed)
                    return;

                if (progress == null || size == null)
                {
                    this.ProgressBar.IsIndeterminate = true;
                    this.PercentageTextBlock.Text = string.Empty;
                }
                else
                {
                    this.ProgressBar.IsIndeterminate = false;
                    this.ProgressBar.Value = progress.Value;
                    this.PercentageTextBlock.Text = $"{progress.Value:0}% ({ApiHelpers.BytesToString(downloaded)}/{ApiHelpers.BytesToString(size.Value)})";
                }
            });
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

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}