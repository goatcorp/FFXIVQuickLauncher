using System;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Common.Util;
using XIVLauncher.Windows.ViewModel;
using XIVLauncher.Xaml;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for GameRepairProgressWindow.xaml
    /// </summary>
    public partial class GameRepairProgressWindow : Window
    {
        private readonly PatchVerifier _verify;

        private readonly Timer _timer;

        private GameRepairProgressWindowViewModel ViewModel => DataContext as GameRepairProgressWindowViewModel;

        public GameRepairProgressWindow(PatchVerifier verify)
        {
            InitializeComponent();

            _verify = verify;

            this.DataContext = new GameRepairProgressWindowViewModel();

            MouseMove += GameRepairProgressWindow_OnMouseMove;
            Closing += GameRepairProgressWindow_OnClosing;

            ViewModel.CancelCommand = new SyncCommand(CancelButton_OnCommand);

            _timer = new Timer();
            _timer.Elapsed += ViewUpdateTimerOnElapsed;
            _timer.AutoReset = true;
            _timer.Interval = 20;

            IsVisibleChanged += (_, _) =>
            {
                _timer.Enabled = IsVisible;
                if (IsVisible)
                    UpdateStatusDisplay();
            };
            Closed += (_, _) => _timer.Dispose();
        }

        private void CancelButton_OnCommand(object p)
        {
            CancelButton.IsEnabled = false;
            _verify.Cancel().ConfigureAwait(false);
        }

        private void GameRepairProgressWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void GameRepairProgressWindow_OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;

            if (CancelButton.IsEnabled)
            {
                CancelButton.IsEnabled = false;
                _ = _verify.Cancel();
            }
        }

        private void UpdateStatusDisplay()
        {
            _timer.Interval = _verify.ProgressUpdateInterval == TimeSpan.Zero ? 100 : _verify.ProgressUpdateInterval.TotalMilliseconds;

            switch (_verify.State)
            {
                case PatchVerifier.VerifyState.DownloadMeta:
                    CurrentStepText.Text = ViewModel.DownloadingMetaLoc;
                    InfoTextBlock.Text = $"{_verify.CurrentFile}";
                    StatusTextBlock.Text = $"{Math.Min(_verify.PatchSetIndex + 1, _verify.PatchSetCount)}/{_verify.PatchSetCount} - {ApiHelpers.BytesToString(this._verify.Progress)}/{ApiHelpers.BytesToString(_verify.Total)}";
                    SpeedTextBlock.Text = string.Format(ViewModel.SpeedUnitPerSecLoc, ApiHelpers.BytesToString(_verify.Speed));
                    EstimatedTimeTextBlock.Text = ViewModel.FormatEstimatedTime(_verify.Total - _verify.Progress, _verify.Speed);
                    this.Progress.Value = _verify.Total != 0 ? 100.0 * _verify.Progress / _verify.Total : 0;
                    break;

                case PatchVerifier.VerifyState.VerifyAndRepair:
                    CurrentStepText.Text = _verify.CurrentMetaInstallState switch
                    {
                        Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.NotStarted => ViewModel.VerifyingLoc,
                        _ => ViewModel.RepairingLoc,
                    };

                    InfoTextBlock.Text = $"{_verify.CurrentFile}";

                    StatusTextBlock.Text = $"{Math.Min(_verify.PatchSetIndex + 1, _verify.PatchSetCount)}/{_verify.PatchSetCount} - {Math.Min(_verify.TaskIndex + 1, _verify.TaskCount)}/{_verify.TaskCount} - {ApiHelpers.BytesToString(this._verify.Progress)}/{ApiHelpers.BytesToString(_verify.Total)}";

                    SpeedTextBlock.Text = _verify.CurrentMetaInstallState switch
                    {
                        Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.WaitingForReattempt => ViewModel.ReattemptWaitingLoc,
                        Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.Connecting => ViewModel.ConnectingLoc,
                        Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.Finishing => ViewModel.FinishingLoc,
                        _ => string.Format(ViewModel.SpeedUnitPerSecLoc, ApiHelpers.BytesToString(_verify.Speed)),
                    };

                    EstimatedTimeTextBlock.Text = _verify.CurrentMetaInstallState switch
                    {
                        Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.WaitingForReattempt => "",
                        Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.Connecting => "",
                        Common.Patching.IndexedZiPatch.IndexedZiPatchInstaller.InstallTaskState.Finishing => "",
                        _ => ViewModel.FormatEstimatedTime(_verify.Total - _verify.Progress, _verify.Speed),
                    };

                    this.Progress.Value = _verify.Total != 0 ? 100.0 * _verify.Progress / _verify.Total : 0;
                    break;

                default:
                    CurrentStepText.Text = "";
                    InfoTextBlock.Text = "";
                    StatusTextBlock.Text = $"{Math.Min(_verify.TaskIndex + 1, _verify.TaskCount)}/{_verify.TaskCount}";
                    SpeedTextBlock.Text = "";
                    EstimatedTimeTextBlock.Text = "";
                    this.Progress.Value = _verify.State == PatchVerifier.VerifyState.Done ? this.Progress.Maximum : 0;
                    break;
            }
        }

        private void ViewUpdateTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (_verify == null)
                return;

            this.Dispatcher.Invoke(UpdateStatusDisplay);
        }
    }
}
