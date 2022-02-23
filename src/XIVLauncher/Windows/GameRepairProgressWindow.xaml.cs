using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using XIVLauncher.Common;
using XIVLauncher.Common.Game.Patch;
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
            this._verify = verify;
            InitializeComponent();

            this.DataContext = new GameRepairProgressWindowViewModel();

            MouseMove += GameRepairProgressWindow_OnMouseMove;
            Closing += GameRepairProgressWindow_OnClosing;

            ViewModel.CancelCommand = new AsyncCommand(CancelButton_OnCommand);

            UpdateStatusDisplay();
            _timer = new Timer();
            _timer.Elapsed += ViewUpdateTimerOnElapsed;
            _timer.AutoReset = true;
            _timer.Interval = verify.ProgressUpdateInterval;
            _timer.Enabled = true;
            _timer.Start();
        }

        private async Task CancelButton_OnCommand(object p)
        {
            CancelButton.IsEnabled = false;
            await _verify.Cancel();
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
            if (_verify.IsInstalling)
            {
                this.CurrentStepText.Text = ViewModel.RepairingLoc;
            }
            else
            {
                this.CurrentStepText.Text = ViewModel.VerifyingLoc;
            }

            InfoTextBlock.Text = $"{_verify.CurrentFile}";
            StatusTextBlock.Text = $"{Math.Min(_verify.PatchSetIndex + 1, _verify.PatchSetCount)}/{_verify.PatchSetCount} - {Math.Min(_verify.TaskIndex + 1, _verify.TaskCount)}/{_verify.TaskCount} - {Util.BytesToString(this._verify.Progress)}/{Util.BytesToString(_verify.Total)}";
            SpeedTextBlock.Text = string.Format(ViewModel.SpeedUnitPerSecLoc, Util.BytesToString(_verify.Speed));
            EstimatedTimeTextBlock.Text = ViewModel.FormatEstimatedTime(_verify.Total - _verify.Progress, _verify.Speed);
            this.Progress.Value = _verify.Total != 0 ? 100.0 * _verify.Progress / _verify.Total : 0;
        }

        private void ViewUpdateTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(UpdateStatusDisplay);
        }

        public void StopTimer()
        {
            _timer.Stop();
        }
    }
}