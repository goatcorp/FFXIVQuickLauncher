using CheapLoc;
using System.Windows;
using System.Windows.Input;
using XIVLauncher.Common.Game;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for ConfigImportExportProgressWindow.xaml
    /// </summary>
    public partial class ConfigImportExportProgressWindow : Window
    {
        public ConfigImportExportProgressWindow()
        {
            InitializeComponent();

            this.DataContext = new ConfigImportExportProgressWindowViewModel();

            MouseMove += ConfigImportExportProgressWindow_OnMouseMove;
        }

        private void ConfigImportExportProgressWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        public void SetTitle(string newText)
        {
            InfoTextBlock.Text = $"{newText}";
        }
    }
}
