using System.Windows;
using XIVLauncher.Game;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class IntegrityCheckProgressWindow : Window
    {
        public IntegrityCheckProgressWindow()
        {
            InitializeComponent();
        }

        public void UpdateProgress(IntegrityCheck.IntegrityCheckProgress progress)
        {
            InfoTextBlock.Text = $"{progress.CurrentFile}";
        }
    }
}