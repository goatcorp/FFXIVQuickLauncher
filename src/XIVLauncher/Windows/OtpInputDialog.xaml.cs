using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Serilog;
using XIVLauncher.Http;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for OtpInputDialog.xaml
    /// </summary>
    public partial class OtpInputDialog : Window
    {
        public string Result { get; private set; }

        private OtpListener _otpListener;

        public OtpInputDialog()
        {
            InitializeComponent();

            this.DataContext = new OtpInputDialogViewModel();

            MouseMove += OtpInputDialog_OnMouseMove;

            OtpTextBox.Focus();

            if (App.Settings.OtpServerEnabled)
            {
                _otpListener = new OtpListener();
                _otpListener.OnOtpReceived += otp =>
                {
                    Result = otp;
                    Dispatcher.Invoke(() =>
                    {
                        Close();
                        _otpListener?.Stop();
                    });
                };

                try
                {
                    // Start Listen
                    Task.Run(() => _otpListener.Start());
                    Log.Debug("OTP server started...");
                }
                catch(Exception ex)
                {
                    Log.Error(ex, "Could not start OTP HTTP listener.");
                }
            }
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
            if ((e.Key != Key.Enter && e.Key != Key.Return) || OtpTextBox.Text.Length != 6) 
                return;

            Result = OtpTextBox.Text;
            _otpListener?.Stop();
            Close();
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (OtpTextBox.Text.Length != 6)
                return;

            Result = OtpTextBox.Text;
            _otpListener?.Stop();
            Close();
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            _otpListener?.Stop();
            Close();
        }

        public void OpenShortcutInfo_MouseUp(object sender, RoutedEventArgs e)
        {
            Process.Start($"https://goatcorp.github.io/faq/mobile_otp");
        }
    }
}
