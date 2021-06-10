using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using Serilog;
using XIVLauncher.Game.Patch;
using XIVLauncher.Http;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for PatchDownloadDialog.xaml
    /// </summary>
    public partial class PatchDownloadDialog : Window
    {
        private readonly PatchManager _manager;

        public PatchDownloadDialogViewModel ViewModel => DataContext as PatchDownloadDialogViewModel;

        public PatchDownloadDialog(PatchManager manager)
        {
            _manager = manager;
            InitializeComponent();
            this.DataContext = new PatchDownloadDialogViewModel();

            var viewUpdateTimer = new Timer();
            viewUpdateTimer.Elapsed += ViewUpdateTimerOnElapsed;
            viewUpdateTimer.AutoReset = true;
            viewUpdateTimer.Interval = 200;
            viewUpdateTimer.Enabled = true;
            viewUpdateTimer.Start();
        }

        private void ViewUpdateTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                SetGeneralProgress(_manager.CurrentInstallIndex, _manager.Downloads.Count);

                for (var i = 0; i < PatchManager.MAX_DOWNLOADS_AT_ONCE; i++)
                {
                    var activePatch = _manager.Actives[i];

                    if (_manager.Slots[i] || activePatch == null)
                    {
                        SetPatchProgress(i, ViewModel.PatchDoneLoc, 100f);
                        continue;
                    }

                    var pct = Math.Round((double) (100 * _manager.Progresses[i]) / activePatch.Patch.Length, 2);
                    SetPatchProgress(i,
                        $"{activePatch.Patch} ({pct:#0.00}%, {Util.BytesToString(_manager.Speeds[i])}/s)",
                        pct);
                }

                if (_manager.DownloadsDone)
                {
                    SetLeft(0, 0);
                    SetDownloadDone();
                }
                else
                {
                    SetLeft(_manager.AllDownloadsLength, _manager.Speeds.Sum());
                }
            });
        }

        public void SetGeneralProgress(int curr, int final)
        {
            PatchProgressText.Text = string.Format(ViewModel.PatchGeneralStatusLoc,
                $"{curr}/{final}");

            InstallingText.Text = string.Format(ViewModel.PatchInstallingFormattedLoc, curr);
        }

        public void SetLeft(long left, double rate)
        {
            BytesLeftText.Text = string.Format(ViewModel.PatchEtaLoc, Util.BytesToString(left), Util.BytesToString(rate));
        }

        public void SetPatchProgress(int index, string patchName, double pct)
        {
            switch (index)
            {
                case 0:
                    SetProgressBar1Progress(patchName, pct);
                    break;
                case 1:
                    SetProgressBar2Progress(patchName, pct);
                    break;
                case 2:
                    SetProgressBar3Progress(patchName, pct);
                    break;
                case 3:
                    SetProgressBar4Progress(patchName, pct);
                    break;
            }
        }

        public void SetProgressBar1Progress(string patchName, double percentage)
        {
            Progress1.Value = percentage;
            Progress1Text.Text = patchName;
        }

        public void SetProgressBar2Progress(string patchName, double percentage)
        {
            Progress2.Value = percentage;
            Progress2Text.Text = patchName;
        }

        public void SetProgressBar3Progress(string patchName, double percentage)
        {
            Progress3.Value = percentage;
            Progress3Text.Text = patchName;
        }

        public void SetProgressBar4Progress(string patchName, double percentage)
        {
            Progress4.Value = percentage;
            Progress4Text.Text = patchName;
        }

        public void SetDownloadDone()
        {
            Progress1.Visibility = Visibility.Collapsed;
            Progress1Text.Visibility = Visibility.Collapsed;

            Progress2.Visibility = Visibility.Collapsed;
            Progress2Text.Visibility = Visibility.Collapsed;

            Progress3.Visibility = Visibility.Collapsed;
            Progress3Text.Visibility = Visibility.Collapsed;

            Progress4.Visibility = Visibility.Collapsed;
            Progress4Text.Visibility = Visibility.Collapsed;
        }

        private void PatchDownloadDialog_OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true; // We can't cancel patching yet, big TODO
        }

        private void BytesLeftText_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
#if DEBUG
            _manager.CancelAllDownloads();
#endif
        }
    }
}
