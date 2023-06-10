using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Serilog;
using XIVLauncher.Common.Http;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for OtpInputDialog.xaml
    /// </summary>
    public partial class OtpInputDialog : Window
    {
        public event Action<string> OnResult;

        private readonly Brush _otpInputPromptDefaultBrush;

        private OtpInputDialogViewModel ViewModel => DataContext as OtpInputDialogViewModel;

        private OtpListener _otpListener;
        private bool _ignoreCurrentOtp;

        public OtpInputDialog()
        {
            InitializeComponent();

            _otpInputPromptDefaultBrush = OtpInputPrompt.Foreground;

            this.DataContext = new OtpInputDialogViewModel();

            MouseMove += OtpInputDialog_OnMouseMove;
            Activated += (_, _) => OtpTextBox.Focus();
            GotFocus += (_, _) => OtpTextBox.Focus();
        }

        public new bool? ShowDialog()
        {
            OtpTextBox.Focus();

            if (App.Settings.OtpServerEnabled)
            {
                _otpListener = new OtpListener("legacy-" + AppUtil.GetAssemblyVersion());
                _otpListener.OnOtpReceived += TryAcceptOtp;

                try
                {
                    // Start Listen
                    Task.Run(() => _otpListener.Start());
                    Log.Debug("OTP server started...");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Could not start OTP HTTP listener.");
                }
            }

            return base.ShowDialog();
        }

        public void Reset()
        {
            OtpInputPrompt.Text = ViewModel.OtpInputPromptLoc;
            OtpInputPrompt.Foreground = _otpInputPromptDefaultBrush;
            OtpTextBox.Text = "";
            OtpTextBox.Focus();
        }

        public void IgnoreCurrentResult(string reason)
        {
            OtpInputPrompt.Text = reason;
            OtpInputPrompt.Foreground = Brushes.Red;
            _ignoreCurrentOtp = true;
        }

        public void TryAcceptOtp(string otp)
        {
            if (otp.Length != 6)
            {
                Log.Error("Malformed OTP: {Otp}", otp);

                Dispatcher.Invoke(() =>
                {
                    OtpInputPrompt.Text = ViewModel.OtpInputPromptBadLoc;
                    OtpInputPrompt.Foreground = Brushes.Red;
                    Storyboard myStoryboard = (Storyboard)OtpInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), OtpInputPrompt);
                    myStoryboard.Begin();
                    OtpTextBox.Focus();
                });

                return;
            }

            _ignoreCurrentOtp = false;
            OnResult?.Invoke(otp);

            Dispatcher.Invoke(() =>
            {
                if (_ignoreCurrentOtp)
                {
                    Storyboard myStoryboard = (Storyboard)OtpInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), OtpInputPrompt);
                    myStoryboard.Begin();
                    OtpTextBox.Focus();
                }
                else
                {
                    _otpListener?.Stop();
                    DialogResult = true;
                    Hide();
                }
            });
        }

        private void Cancel()
        {
            OnResult?.Invoke(null);
            _otpListener?.Stop();
            DialogResult = false;
            Hide();
        }

        private void OtpInputDialog_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void OtpTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void OtpTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void OtpTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancel();
            }
            else if (e.Key == Key.Enter)
            {
                TryAcceptOtp(this.OtpTextBox.Text);
            }
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            TryAcceptOtp(this.OtpTextBox.Text);
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void PasteButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.OtpTextBox.Text = Clipboard.GetText();
            TryAcceptOtp(this.OtpTextBox.Text);
        }

        public void OpenShortcutInfo_MouseUp(object sender, RoutedEventArgs e)
        {
            Process.Start($"https://goatcorp.github.io/faq/mobile_otp");
        }

        public static string AskForOtp(Action<OtpInputDialog, string> onOtpResult, Window parentWindow)
        {
            if (Dispatcher.CurrentDispatcher != parentWindow.Dispatcher)
                return parentWindow.Dispatcher.Invoke(() => AskForOtp(onOtpResult, parentWindow));

            var dialog = new OtpInputDialog();
            if (parentWindow.IsVisible)
            {
                dialog.Owner = parentWindow;
                dialog.ShowInTaskbar = false;
            }

            string result = null;
            dialog.OnResult += otp => onOtpResult(dialog, result = otp);
            return dialog.ShowDialog() == true ? result : null;
        }
    }
}