using System.Windows;
using System.Windows.Input;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for OtpInputDialog.xaml
    /// </summary>
    public partial class UpdateLoadingDialog : Window
    {
        public UpdateLoadingDialog()
        {
            InitializeComponent();

            AutoLoginDisclaimer.Visibility = App.Settings.AutologinEnabled ? Visibility.Visible : Visibility.Collapsed;
            ResetUidCacheDisclaimer.Visibility = App.Settings.UniqueIdCacheEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (ResetUidCacheDisclaimer.Visibility == Visibility.Visible
                && AutoLoginDisclaimer.Visibility == Visibility.Visible) {
                UpdateLoadingCard.Height += 19;
            }

            this.DataContext = new UpdateLoadingDialogViewModel();

            MouseMove += UpdateLoadingDialog_OnMouseMove;
        }

        private void UpdateLoadingDialog_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
