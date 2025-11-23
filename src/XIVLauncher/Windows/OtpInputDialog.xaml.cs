using System;
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
using XIVLauncher.Common.Util;
using XIVLauncher.Windows.ViewModel;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for OtpInputDialog.xaml
    /// </summary>
    public partial class OtpInputDialog : Window
    {
        public event Action<string, string> OnResult;

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
            Topmost = App.Settings.OtpAlwaysOnTopEnabled;

            // Initialize reCAPTCHA WebView2
            RecaptchaWebView.WebMessageReceived += RecaptchaWebView_WebMessageReceived;
            this.Loaded += OtpInputDialog_Loaded;
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
            OnResult?.Invoke(otp, recaptchaToken.Text);

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
            OnResult?.Invoke(null, null);
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
            PlatformHelpers.OpenBrowser($"https://goatcorp.github.io/faq/mobile_otp");
        }

        public static string AskForOtp(Action<OtpInputDialog, string, string> onOtpResult, Window parentWindow)
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
            dialog.OnResult += (otp, token) => onOtpResult(dialog, result = otp, token);
            return dialog.ShowDialog() == true ? result : null;
        }

        private async void OtpInputDialog_Loaded(object sender, RoutedEventArgs e)
        {
            await RecaptchaWebView.EnsureCoreWebView2Async();
            
            // Create a temporary file for the HTML
            var tempDir = Path.GetTempPath();
            var tempFile = Path.Combine(tempDir, "recaptcha_page.html");
            var htmlPath = Path.Combine(AppContext.BaseDirectory, "Resources", "recaptcha_page.html");
            var htmlContent = File.ReadAllText(htmlPath);
            File.WriteAllText(tempFile, htmlContent);
            
            // Set up virtual host mapping
            RecaptchaWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("user.ffxiv.com.tw", tempDir, Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            
            // Navigate to the virtual host
            RecaptchaWebView.Source = new Uri("https://user.ffxiv.com.tw/recaptcha_page.html");
            
            // Clean up temp file when dialog closes
            this.Closed += (s, args) => { try { File.Delete(tempFile); } catch { } };
        }

        private void RecaptchaWebView_WebMessageReceived(object sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                var payload = ParseRecaptchaMessage(json);
                if (payload != null && payload.TryGetValue("token", out var token))
                {
                    recaptchaToken.Text = token;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to parse reCAPTCHA token");
            }
        }

        private Dictionary<string, string> ParseRecaptchaMessage(string webMessageAsJson)
        {
            try
            {
                // WebMessageAsJson is a JSON string containing another JSON string, so deserialize twice
                var innerJson = JsonSerializer.Deserialize<string>(webMessageAsJson);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(innerJson);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to parse reCAPTCHA message");
                return null;
            }
        }
    }
}
