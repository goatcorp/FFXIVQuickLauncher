using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class OtpInputDialog : Window
    {
        public string Result { get; private set; } = null;

        public OtpInputDialog()
        {
            InitializeComponent();

            OtpTextBox.Focus();
        }

        private void OtpTextBox_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void OtpTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                Result = OtpTextBox.Text;
                Close();
            }
        }

        private void OkButton_OnClick(object sender, RoutedEventArgs e)
        {
            Result = OtpTextBox.Text;
            Close();
        }

        private void CancelButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
