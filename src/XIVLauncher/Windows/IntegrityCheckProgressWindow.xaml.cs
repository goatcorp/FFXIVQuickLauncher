using System.Windows;
using XIVLauncher.Game;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class IntegrityCheckProgressWindow
    {
        public IntegrityCheckProgressWindow()
        {
            InitializeComponent();

            this.DataContext = new IntegrityCheckProgressWindowViewModel();
        }

        public void UpdateProgress(IntegrityCheck.IntegrityCheckProgress progress)
        {
            InfoTextBlock.Text = $"{progress.CurrentFile}";
        }
    }
}