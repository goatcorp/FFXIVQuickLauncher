using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Serilog;
using XIVLauncher.Accounts;
using XIVLauncher.Windows.ViewModel;
using Yubico.YubiKey;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for SecurityKeySetupDialog.xaml
    /// </summary>
    public partial class SecurityKeySetupDialog : Window
    {
        public event Action<string> OnResult;

        private readonly Brush _setupPromptDefaultBrush;

        private SecurityKeySetupDialogViewModel ViewModel => DataContext as SecurityKeySetupDialogViewModel;

        private bool _usernameProvided;

        private YubiAuth _yubiAuth;

        public SecurityKeySetupDialog()
        {
            InitializeComponent();
            _setupPromptDefaultBrush = KeySetupInputPrompt.Foreground;

            this.DataContext = new SecurityKeySetupDialogViewModel();
            DataObject.AddPastingHandler(KeySetupTextBox, KeySetupTextBox_OnPaste);

            MouseMove += KeySetupDialog_OnMouseMove;
            Activated += (_, _) => KeySetupTextBox.Focus();
            GotFocus += (_, _) => KeySetupTextBox.Focus();
        }

        public new bool? ShowDialog()
        {
            KeySetupTextBox.Focus();

            _yubiAuth = new YubiAuth();
            if (_yubiAuth.GetYubiDevice() != null)
            {
                Dispatcher.Invoke(() =>
                {
                    KeySetupInputPrompt.Text = ViewModel.KeySetupInputPromptYubiLoc;
                    KeySetupInputPrompt.Foreground = Brushes.LightGreen;
                    Storyboard myStoryboard = (Storyboard)KeySetupInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), KeySetupInputPrompt);
                    myStoryboard.Begin();
                    KeySetupTextBox.Focus();
                });
            }

            AccountManager accountManager = new AccountManager(App.Settings);

            var savedAccount = accountManager.CurrentAccount;

            if (savedAccount != null)
            {
                YubiAuth.SetUsername(savedAccount.UserName);
                _usernameProvided = true;
            }

            SetupYubiListener();
            return base.ShowDialog();
        }

        private void SetupYubiListener()
        {
            if(_yubiAuth.DeviceListener != null)
            {
                _yubiAuth.DeviceListener.Arrived += OnYubiKeyArrived;
                _yubiAuth.DeviceListener.Removed += OnYubiKeyRemoved;
            }
        }

        private void OnYubiKeyArrived(object sender, YubiKeyDeviceEventArgs e)
        {
            Log.Debug("YubiKey arrived! " + e.Device.ToString());
            _yubiAuth.SetYubiDevice(e.Device);
            ResetPrompt();
        }

        private void OnYubiKeyRemoved(object sender, YubiKeyDeviceEventArgs e)
        {
            Log.Debug("YubiKey removed! " + e.Device.ToString());
            _yubiAuth.SetYubiDevice(null);
            ResetPrompt();
        }
        public void ResetPrompt()
        {
            Dispatcher.Invoke(() =>
            {
                if (_yubiAuth.GetYubiDevice() == null)
                {
                    KeySetupInputPrompt.Text = ViewModel.KeySetupInputPromptInsertLoc;
                    KeySetupInputPrompt.Foreground = _setupPromptDefaultBrush;
                    KeySetupTextBox.Focus();
                }
                else
                {
                    KeySetupInputPrompt.Text = ViewModel.KeySetupInputPromptYubiLoc;
                    KeySetupInputPrompt.Foreground = Brushes.LightGreen;
                    Storyboard myStoryboard = (Storyboard)KeySetupInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), KeySetupInputPrompt);
                    myStoryboard.Begin();
                    KeySetupTextBox.Focus();
                }
            });

        }
        private void Cancel()
        {
            DialogResult = false;
            CleanupYubi();
            OnResult?.Invoke(null);
            Hide();
        }
        private void CleanupYubi()
        {
            if (_yubiAuth.DeviceListener != null)
            {
                _yubiAuth.DeviceListener.Arrived -= OnYubiKeyArrived;
                _yubiAuth.DeviceListener.Removed -= OnYubiKeyRemoved;
            }
        }

        public void TryAcceptKey(string key)
        {
            if (!_usernameProvided)
            {
                Log.Error("Didn't receive a username.");
                Dispatcher.Invoke(() =>
                {
                    KeySetupInputPrompt.Text = ViewModel.KeySetupUsernameLoc;
                    KeySetupInputPrompt.Foreground = Brushes.Red;
                    Storyboard myStoryboard = (Storyboard)KeySetupInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), KeySetupInputPrompt);
                    myStoryboard.Begin();
                    KeySetupTextBox.Focus();
                });

                return;
            }

            if (key.Length < 32 || Regex.IsMatch(key, "[^A-Za-z2-7=]+"))
            {
                Log.Error("Malformed Authentication Key: {Key}", key);

                Dispatcher.Invoke(() =>
                {
                    KeySetupInputPrompt.Text = ViewModel.KeySetupInputPromptBadLoc;
                    KeySetupInputPrompt.Foreground = Brushes.Red;
                    Storyboard myStoryboard = (Storyboard)KeySetupInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), KeySetupInputPrompt);
                    myStoryboard.Begin();
                    KeySetupTextBox.Focus();
                });

                return;
            }

            var credential = _yubiAuth.BuildCredential(key, KeySetupTouchCheckBox.IsChecked.GetValueOrDefault());
            _yubiAuth.CreateEntry(credential);
            DialogResult = true;
            CleanupYubi();
            OnResult?.Invoke(credential.Name);
            Hide();
        }

        private void KeySetupDialog_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void KeySetupTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^A-Za-z2-7=]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void KeySetupTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void KeySetupTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Cancel();
            }
            else if (e.Key == Key.Enter)
            {
                TryAcceptKey(this.KeySetupTextBox.Text);
            }
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_yubiAuth.GetYubiDevice() == null)
            {
                Dispatcher.Invoke(() =>
                {
                    KeySetupInputPrompt.Text = ViewModel.KeySetupInputPromptInsertLoc;
                    KeySetupInputPrompt.Foreground = Brushes.Red;
                    Storyboard myStoryboard = (Storyboard)KeySetupInputPrompt.Resources["InvalidShake"];
                    Storyboard.SetTarget(myStoryboard.Children.ElementAt(0), KeySetupInputPrompt);
                    myStoryboard.Begin();
                    KeySetupTextBox.Focus();
                });
                return;
            }

            TryAcceptKey(this.KeySetupTextBox.Text);
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        public void OpenShortcutInfo_MouseUp(object sender, RoutedEventArgs e)
        {
            Process.Start($"https://goatcorp.github.io/faq/yubikey");
        }

        public static string AskForKey(Action<SecurityKeySetupDialog, string> onResult, Window parentWindow)
        {
            if (Dispatcher.CurrentDispatcher != parentWindow.Dispatcher)
                return parentWindow.Dispatcher.Invoke(() => AskForKey(onResult, parentWindow));

            var dialog = new SecurityKeySetupDialog();
            if (parentWindow.IsVisible)
            {
                dialog.Owner = parentWindow;
                dialog.ShowInTaskbar = false;
            }

            string result = null;
            dialog.OnResult += credName => onResult(dialog, result = credName);
            return dialog.ShowDialog() == true ? result : null;
        }
        private void KeySetupTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText) return;

            var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            text = text.Replace(" ", "");
            KeySetupTextBox.Text = text;
        }
        private void KeySetupTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}
