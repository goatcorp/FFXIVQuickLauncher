using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using XIVLauncher;
using XIVLauncher.Addon;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class GenericAddonSetup : Window
    {
        public GenericAddon Result { get; private set; }

        public GenericAddonSetup(GenericAddon addon = null)
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
            if(string.IsNullOrWhiteSpace(PathEntry.Text))
                Close();

            Result = new GenericAddon()
            {
                Path = PathEntry.Text,
                CommandLine = CommandLineTextBox.Text,
                RunAsAdmin = AdminCheckBox.IsChecked == true
            };

            Close();
        }
    }
}
