using System.Windows;
using System.Windows.Input;
using XIVLauncher.Common.Game;
using XIVLauncher.Windows.ViewModel;

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

            this.DataContext = new IntegrityCheckProgressWindowViewModel();

            MouseMove += IntegrityCheckProgressWindow_OnMouseMove;
        }

        private void IntegrityCheckProgressWindow_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        public void UpdateProgress(IntegrityCheck.IntegrityCheckProgress progress)
        {
            InfoTextBlock.Text = $"{progress.CurrentFile}";
        }
    }
}