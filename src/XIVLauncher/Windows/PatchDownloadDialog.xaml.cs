using System;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Common.Util;
using XIVLauncher.Windows.ViewModel;
using Brushes = System.Windows.Media.Brushes;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for PatchDownloadDialog.xaml
    /// </summary>
    public partial class PatchDownloadDialog : Window
    {
        private readonly PatchManager _manager;

        private readonly Timer _timer;

        public PatchDownloadDialogViewModel ViewModel => DataContext as PatchDownloadDialogViewModel;

        public PatchDownloadDialog(PatchManager manager)
        {
            InitializeComponent();

            _manager = manager;

            this.DataContext = new PatchDownloadDialogViewModel();

            MouseMove += PatchDownloadDialog_OnMouseMove;

            _timer = new Timer();
            _timer.Elapsed += ViewUpdateTimerOnElapsed;
            _timer.AutoReset = true;
            _timer.Interval = 200;

            IsVisibleChanged += (_, _) => _timer.Enabled = IsVisible;
            Closed += (_, _) => _timer.Dispose();
        }

        private void PatchDownloadDialog_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void ViewUpdateTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (_manager == null)
                return;

            this.Dispatcher.Invoke(() =>
            {
                SetGeneralProgress(_manager.CurrentInstallIndex, _manager.Downloads.Count, this._manager.IsInstallerBusy);

                for (var i = 0; i < PatchManager.MAX_DOWNLOADS_AT_ONCE; i++)
                {
                    var activePatch = _manager.Actives[i];

                    if (_manager.Slots[i] == PatchManager.SlotState.Done || activePatch == null)
                    {
                        SetPatchProgress(i, ViewModel.PatchDoneLoc, 100f, false);
                        continue;
                    }

                    if (_manager.Slots[i] == PatchManager.SlotState.Checking)
                    {
                        SetPatchProgress(i,
                                         $"{activePatch.Patch} ({ViewModel.PatchCheckingLoc})", 100f, true);
                    }
                    else
                    {
                        var pct = Math.Round((double) (100 * _manager.Progresses[i]) / activePatch.Patch.Length, 2);
                        SetPatchProgress(i,
                                         $"{activePatch.Patch} ({pct:#0.0}%, {ApiHelpers.BytesToString(_manager.Speeds[i])}/s)",
                                         pct, false);
                    }
                }

                if (_manager.DownloadsDone)
                {
                    SetLeft(0, 0);
                }
                else
                {
                    SetLeft(_manager.AllDownloadsLength < 0 ? 0 : _manager.AllDownloadsLength, _manager.Speeds.Sum());
                }
            });
        }

        public void SetGeneralProgress(int curr, int final, bool busy)
        {
            PatchProgressText.Text = string.Format(ViewModel.PatchGeneralStatusLoc, $"{curr}/{final}");
            InstallingText.Text = busy ? string.Format(ViewModel.PatchInstallingFormattedLoc, curr) : ViewModel.PatchInstallingIdleLoc;
        }

        public void SetLeft(long left, double rate)
        {
            TimeSpan eta = rate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(left / rate);
            BytesLeftText.Text = string.Format(ViewModel.PatchEtaLoc, ApiHelpers.BytesToString(left), ApiHelpers.BytesToString(rate));
            TimeLeftText.Text = ApiHelpers.GetTimeLeft(eta, ViewModel.PatchEtaTimeLoc);
        }

        public void SetPatchProgress(int index, string patchName, double pct, bool indeterminate)
        {
            switch (index)
            {
                case 0:
                    SetProgressBar1Progress(patchName, pct, indeterminate);
                    break;
                case 1:
                    SetProgressBar2Progress(patchName, pct, indeterminate);
                    break;
                case 2:
                    SetProgressBar3Progress(patchName, pct, indeterminate);
                    break;
                case 3:
                    SetProgressBar4Progress(patchName, pct, indeterminate);
                    break;
            }
        }

        public void SetProgressBar1Progress(string patchName, double percentage, bool indeterminate)
        {
            Progress1.Value = percentage;
            Progress1.IsIndeterminate = indeterminate;
            Progress1Text.Text = patchName;

            this.Progress1.Foreground = Brushes.DodgerBlue;
            this.Progress1.Background = Brushes.LightSkyBlue;
            this.Progress1.BorderBrush = Brushes.LightSkyBlue;
        }

        public void SetProgressBar2Progress(string patchName, double percentage, bool indeterminate)
        {
            Progress2.Value = percentage;
            Progress2.IsIndeterminate = indeterminate;
            Progress2Text.Text = patchName;

            this.Progress2.Foreground = Brushes.DodgerBlue;
            this.Progress2.Background = Brushes.LightSkyBlue;
            this.Progress2.BorderBrush = Brushes.LightSkyBlue;
        }

        public void SetProgressBar3Progress(string patchName, double percentage, bool indeterminate)
        {
            Progress3.Value = percentage;
            Progress3.IsIndeterminate = indeterminate;
            Progress3Text.Text = patchName;

            this.Progress3.Foreground = Brushes.DodgerBlue;
            this.Progress3.Background = Brushes.LightSkyBlue;
            this.Progress3.BorderBrush = Brushes.LightSkyBlue;
        }

        public void SetProgressBar4Progress(string patchName, double percentage, bool indeterminate)
        {
            Progress4.Value = percentage;
            Progress4.IsIndeterminate = indeterminate;
            Progress4Text.Text = patchName;

            this.Progress4.Foreground = Brushes.DodgerBlue;
            this.Progress4.Background = Brushes.LightSkyBlue;
            this.Progress4.BorderBrush = Brushes.LightSkyBlue;
        }

        private void PatchDownloadDialog_OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true; // We can't cancel patching yet, big TODO
        }
    }
}
