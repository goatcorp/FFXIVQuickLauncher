using System.Windows;
using XIVLauncher.Addon;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class GenericAddonSetupWindow : Window
    {
        public GenericAddon Result { get; private set; }

        public GenericAddonSetupWindow(GenericAddon addon = null)
        {
            InitializeComponent();

            if (addon != null)
            {
                PathEntry.Text = addon.Path;
                CommandLineTextBox.Text = addon.CommandLine;
                AdminCheckBox.IsChecked = addon.RunAsAdmin;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PathEntry.Text))
                Close();

            Result = new GenericAddon
            {
                Path = PathEntry.Text,
                CommandLine = CommandLineTextBox.Text,
                RunAsAdmin = AdminCheckBox.IsChecked == true
            };

            Close();
        }
    }
}