using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CheapLoc;
using Serilog;
using XIVLauncher.Http;
using XIVLauncher.Windows.ViewModel;
using XIVLauncher.Xaml;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for DalamudLoadingOverlay.xaml
    /// </summary>
    public partial class DalamudLoadingOverlay : Window
    {
        public DalamudLoadingOverlay()
        {
            InitializeComponent();

            this.DataContext = new DalamudLoadingOverlayViewModel();
        }

        public enum DalamudLoadingProgress
        {
            Dalamud,
            Assets
        }

        public void SetProgress(DalamudLoadingProgress progress)
        {
            ProgressTextBlock.Text = progress switch
            {
                DalamudLoadingProgress.Dalamud => Loc.Localize("DalamudUpdateDalamud", "Updating Dalamud..."),
                DalamudLoadingProgress.Assets => Loc.Localize("DalamudUpdateDalamud", "Updating assets..."),
                _ => throw new ArgumentOutOfRangeException(nameof(progress), progress, null),
            };
            this.Show();
        }

        private void DalamudLoadingOverlay_OnLoaded(object sender, RoutedEventArgs e)
        {
            HideFromWindowSwitcher.Hide(this);
        }
    }
}
