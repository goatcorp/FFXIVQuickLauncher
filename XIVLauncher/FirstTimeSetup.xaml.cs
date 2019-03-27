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

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class FirstTimeSetup : Window
    {
        public FirstTimeSetup()
        {
            InitializeComponent();

            var detectedPath = Util.TryGamePaths();

            if (detectedPath != null)
            {
                GamePathEntry.Text = detectedPath;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (SetupTabControl.SelectedIndex == 0)
            {
                if (!Util.IsValidFFXIVPath(GamePathEntry.Text))
                {
                    MessageBox.Show("The path you selected is not a valid FFXIV installation", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            if (SetupTabControl.SelectedIndex == 3)
            {
                Settings.SetGamePath(GamePathEntry.Text);
                Settings.SetDx11(Dx11RadioButton.IsChecked == true);
                Settings.SetExpansionLevel(ExpansionLevelComboBox.SelectedIndex);
                Settings.SetLanguage((ClientLanguage) LanguageComboBox.SelectedIndex);
                Settings.Save();
                Close();
            }

            SetupTabControl.SelectedIndex++;
        }
    }
}
