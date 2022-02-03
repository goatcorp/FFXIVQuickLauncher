using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NuGet;
using XIVLauncher.Addon;
using XIVLauncher.Windows.ViewModel;
using CheckBox = System.Windows.Controls.CheckBox;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class OtpUriSetupWindow : Window
    {
        public string Result { get; private set; } = string.Empty;

        CancellationTokenSource _cancellationToken = null;
        Task _otpCodeUpdateTask = null;
        private TokenInfo? _tokenInfo;

        public OtpUriSetupWindow(string uri = null)
        {
            InitializeComponent();

            DataContext = new OtpUriSetupWindowViewModel();

            if (uri != null)
            {
                Result = uri;
                OtpUriTextBox.Text = uri;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OtpUriTextBox.Text))
            {
                Result = string.Empty;
                Close();
            }

            if (!(OtpCodeCheckBox.IsChecked ?? false))
            {
                Result = string.Empty;
                Close();
            }

            Result = OtpUriTextBox.Text;
            Close();
        }

        private async void OtpUriTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.TextBox otpUriTextBox))
                return;

            _cancellationToken?.Cancel();
            await (_otpCodeUpdateTask ?? Task.CompletedTask);

            _cancellationToken?.Dispose();
            _otpCodeUpdateTask?.Dispose();

            _cancellationToken = null;
            _otpCodeUpdateTask = null;

            var unknownValue = (DataContext as OtpUriSetupWindowViewModel)?.UnknownValueLoc ?? "<Unknown>";

            if (string.IsNullOrWhiteSpace(otpUriTextBox.Text))
            {
                SecretCheckBox.IsChecked = false;
                SecretCheckBox.Content = unknownValue;
                SecretCheckBox.Foreground = new SolidColorBrush(Colors.Red);

                PeriodCheckBox.IsChecked = false;
                PeriodCheckBox.Content = unknownValue;
                PeriodCheckBox.Foreground = new SolidColorBrush(Colors.Red);

                LengthCheckBox.IsChecked = false;
                LengthCheckBox.Content = unknownValue;
                LengthCheckBox.Foreground = new SolidColorBrush(Colors.Red);

                AlgorithmCheckBox.IsChecked = false;
                AlgorithmCheckBox.Content = unknownValue;
                AlgorithmCheckBox.Foreground = new SolidColorBrush(Colors.Red);

                OtpCodeCheckBox.IsChecked = false;
                OtpCodeCheckBox.Content = unknownValue;
                OtpCodeCheckBox.Foreground = new SolidColorBrush(Colors.Red);

                return;
            }

            try
            {
                if (Uri.TryCreate(otpUriTextBox.Text, UriKind.Absolute, out Uri uri))
                {
                    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

                    if (!query.AllKeys.Contains("secret"))
                    {
                        await ChangeCheckBox(SecretCheckBox, unknownValue, false);
                        await ChangeCheckBox(PeriodCheckBox, unknownValue, false);
                        await ChangeCheckBox(LengthCheckBox, unknownValue, false);
                        await ChangeCheckBox(AlgorithmCheckBox, unknownValue, false);
                        await ChangeCheckBox(OtpCodeCheckBox, unknownValue, false);

                        return;
                    }

                    var secret = query["secret"];
                    await ChangeCheckBox(SecretCheckBox, secret, true);

                    if (query.AllKeys.Contains("period") && int.TryParse(query["period"], out int period))
                    {
                        await ChangeCheckBox(PeriodCheckBox, period.ToString(), true);
                    }
                    else
                    {
                        period = 30;
                        await ChangeCheckBox(PeriodCheckBox, unknownValue, false);
                    }
                    if (query.AllKeys.Contains("digits") && int.TryParse(query["digits"], out int digits))
                    {
                        await ChangeCheckBox(LengthCheckBox, digits.ToString(), true);
                    }
                    else
                    {
                        digits = 6;
                        await ChangeCheckBox(LengthCheckBox, unknownValue, false);
                    }

                    var algorithm = "sha1";
                    if (query.AllKeys.Contains("algorithm") && new[] { "sha1", "sha256", "sha512" }.Any(a => a.Equals(query["algorithm"], StringComparison.OrdinalIgnoreCase)))
                    {
                        algorithm = query["algorithm"];
                        await ChangeCheckBox(AlgorithmCheckBox, algorithm.ToString().ToUpperInvariant(), true);
                    }
                    else
                    {
                        algorithm = "sha1";
                        await ChangeCheckBox(AlgorithmCheckBox, unknownValue, false);
                    }

                    _tokenInfo = new TokenInfo()
                    {
                        Secret = secret,
                        Algorithm = algorithm,
                        Digits = digits,
                        Period = period
                    };
                }
                else
                {
                    var secret = otpUriTextBox.Text;
                    await ChangeCheckBox(SecretCheckBox, secret, true);
                    await ChangeCheckBox(PeriodCheckBox, unknownValue, false);
                    await ChangeCheckBox(LengthCheckBox, unknownValue, false);
                    await ChangeCheckBox(AlgorithmCheckBox, unknownValue, false);

                    _tokenInfo = new TokenInfo()
                    {
                        Secret = secret,
                        Algorithm = "sha1",
                        Digits = 6,
                        Period = 30
                    };
                }

                _cancellationToken = new CancellationTokenSource();
                _otpCodeUpdateTask = OtpCodeUpdater(_cancellationToken.Token);
            }
            catch (Exception)
            {
                await ChangeCheckBox(OtpCodeCheckBox, unknownValue, false);
            }
        }

        private async Task ChangeCheckBox(CheckBox checkBox, string contents, bool isChecked)
        {
            if (!checkBox.Dispatcher.CheckAccess())
            {
                await checkBox.Dispatcher.InvokeAsync(() => ChangeCheckBox(checkBox, contents, isChecked)).Task;
                return;
            }

            checkBox.IsChecked = isChecked;
            checkBox.Content = contents;
            checkBox.Foreground = new SolidColorBrush(isChecked ? Colors.Green : Colors.Red);
        }

        private async Task OtpCodeUpdater(CancellationToken token)
        {
            var unknownValue = (DataContext as OtpUriSetupWindowViewModel)?.UnknownValueLoc ?? "<Unknown>";

            await Task.Yield();

            while (!token.IsCancellationRequested)
            {
                // No generator exit
                if (_tokenInfo == null)
                    return;

                try
                {
                    await ChangeCheckBox(OtpCodeCheckBox, Util.GetTotpToken(_tokenInfo.Value.Secret, _tokenInfo.Value.Algorithm, _tokenInfo.Value.Digits, _tokenInfo.Value.Period), true);
                }
                catch (Exception)
                {
                    await ChangeCheckBox(OtpCodeCheckBox, unknownValue, false);
                }

                try
                {
                    var remaining = (DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(0)).TotalSeconds % Convert.ToDouble(_tokenInfo?.Period ?? 30);
                    await Task.Delay(TimeSpan.FromSeconds(remaining), token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            _cancellationToken?.Cancel();
            await(_otpCodeUpdateTask ?? Task.CompletedTask);

            _cancellationToken?.Dispose();
            _otpCodeUpdateTask?.Dispose();

            _cancellationToken = null;
            _otpCodeUpdateTask = null;
        }

        private struct TokenInfo
        {
            public string Secret;
            public string Algorithm;
            public int Digits;
            public int Period;
        }
    }
}