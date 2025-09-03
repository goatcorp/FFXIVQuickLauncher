using System.Windows;

namespace XIVLauncher.Windows
{
    public partial class BetaKeyDialog : Window
    {
        public string BetaKey { get; private set; }

        public BetaKeyDialog()
        {
            InitializeComponent();
        }

        private void UnlockButton_Click(object sender, RoutedEventArgs e)
        {
            BetaKey = BetaKeyTextBox.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
