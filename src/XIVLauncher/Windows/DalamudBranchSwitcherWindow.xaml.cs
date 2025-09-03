using System.Windows;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class DalamudBranchSwitcherWindow : Window
    {
        private BranchSwitcherViewModel Model => this.DataContext as BranchSwitcherViewModel;

        public DalamudBranchSwitcherWindow()
        {
            InitializeComponent();
            DataContext = new BranchSwitcherViewModel();
            Loaded += DalamudBranchSwitcherWindow_Loaded;
        }

        private async void DalamudBranchSwitcherWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Model.FetchBranchesAsync();
        }

        private void SwitchBranchButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.SelectedBranch != null)
            {
                App.Settings.DalamudBetaKind = Model.SelectedBranch.Track;
                App.Settings.DalamudBetaKey = Model.SelectedBranch.Key;

                App.DalamudUpdater.Run(
                    App.Settings.DalamudBetaKind,
                    App.Settings.DalamudBetaKey,
                    Updates.HaveFeatureFlag(Updates.LeaseFeatureFlags.ForceProxyDalamudAndAssets));

                this.DialogResult = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please select a branch.", "XIVLauncher - Dalamud Branch Switcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BetaKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new BetaKeyDialog { Owner = this };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.BetaKey))
            {
                Model.AppliedBetaKey = dialog.BetaKey;
                await Model.FetchBranchesAsync();
            }
        }

        private void NeverMindButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
