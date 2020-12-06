using System.Windows;
using XIVLauncher.Accounts;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class ProfilePictureInputWindow : Window
    {
        public string ResultName, ResultWorld;

        public ProfilePictureInputWindow(XivAccount account)
        {
            InitializeComponent();

            DataContext = new ProfilePictureInputWindowViewModel();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            ResultName = CharacterNameTextBox.Text;
            ResultWorld = WorldNameTextBox.Text;

            Close();
        }
    }
}
