using System;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using XIVLauncher.Common;
using XIVLauncher.Game.Patch;
using XIVLauncher.Windows.ViewModel;

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

            _timer = new Timer();
            _timer.Elapsed += ViewUpdateTimerOnElapsed;
            _timer.AutoReset = true;
            _timer.Interval = 200;
            _timer.Enabled = true;
            _timer.Start();
        }

        private void GameRepairProgressWindow_OnClosing(object sender, CancelEventArgs e)
        {
            e.Cancel = true; // We can't cancel patching yet, big TODO
        }

        private void ViewUpdateTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
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
                StatusTextBlock.Text = $"{Math.Min(_verify.TaskIndex + 1, _verify.TaskCount)}/{_verify.TaskCount} - {Util.BytesToString(this._verify.Progress)}/{Util.BytesToString(_verify.Total)}";
                this.Progress.Value = _verify.Total != 0 ? 100.0 * _verify.Progress / _verify.Total : 0;
            });
        }

        public void StopTimer()
        {
            _timer.Stop();
        }
    }
}