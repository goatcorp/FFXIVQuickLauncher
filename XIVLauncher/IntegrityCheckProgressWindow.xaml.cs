using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for FirstTimeSetup.xaml
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
