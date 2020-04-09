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
    public partial class UpdateLoadingDialog : Window
    {
        public UpdateLoadingDialog()
        {
            InitializeComponent();

            AutoLoginDisclaimer.Visibility = App.Settings.AutologinEnabled ? Visibility.Visible : Visibility.Hidden;
            this.DataContext = new UpdateLoadingDialogViewModel();
        }
    }
}
