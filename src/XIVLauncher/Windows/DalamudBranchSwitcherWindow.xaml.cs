using System;
using System.Windows;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class DalamudBranchSwitcherWindow : Window
    {
        private DalamudBranchSwitcherViewModel Model => this.DataContext as DalamudBranchSwitcherViewModel;

        public DalamudBranchSwitcherWindow()
        {
            InitializeComponent();
            DataContext = new DalamudBranchSwitcherViewModel();
            Loaded += DalamudBranchSwitcherWindow_Loaded;
        }

        private async void DalamudBranchSwitcherWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Model.AppliedBetaKey = App.Settings.DalamudBetaKey;
                await this.Model.FetchBranchesAsync();
            }
            catch (Exception ex)
            {
                new CustomMessageBox.Builder()
                    .WithCaption("XIVLauncher - Dalamud Branch Switcher")
                    .WithText("An error occurred while fetching the available Dalamud branches")
                    .WithDescription(ex.ToString())
                    .WithImage(MessageBoxImage.Error)
                    .WithButtons(MessageBoxButton.OK)
                    .Show();

                this.DialogResult = false;
                this.Close();
            }
        }

        private void SwitchBranchButton_Click(object sender, RoutedEventArgs e)
        {
            if (Model.SelectedBranch != null)
            {
                if (!Model.SelectedBranch.IsApplicableForCurrentGameVer.GetValueOrDefault(false))
                {
                    MessageBox.Show("This branch is not available for the current game version.\nDalamud needs to be updated after patches, which may take a while.", "Unavailable Branch", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                App.Settings.DalamudBetaKind = Model.SelectedBranch.Track;
                App.Settings.DalamudBetaKey = Model.SelectedBranch.Key;

                App.DalamudUpdater.Run(
                    App.Settings.DalamudBetaKind,
                    App.Settings.DalamudBetaKey);

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
