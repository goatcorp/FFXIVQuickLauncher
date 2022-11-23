using System;
using System.Windows;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    ///     Interaction logic for FirstTimeSetup.xaml
    /// </summary>
    public partial class AdvancedSettingsWindow : Window
    {
        public bool WasCompleted { get; private set; } = false;

        private readonly string actPath;

        public AdvancedSettingsWindow()
        {
            InitializeComponent();

            this.DataContext = new AdvancedSettingsViewModel();
            Load();
        }

        private void Load()
        {
            UidCacheCheckBox.IsChecked = App.Settings.UniqueIdCacheEnabled;
            ExitLauncherAfterGameExitCheckbox.IsChecked = App.Settings.ExitLauncherAfterGameExit ?? true;
            TreatNonZeroExitCodeAsFailureCheckbox.IsChecked = App.Settings.TreatNonZeroExitCodeAsFailure ?? false;
            ForceNorthAmericaCheckbox.IsChecked = App.Settings.ForceNorthAmerica ?? false;
        }

        private void Save()
        {
            App.Settings.UniqueIdCacheEnabled = UidCacheCheckBox.IsChecked == true;
            App.Settings.ExitLauncherAfterGameExit = ExitLauncherAfterGameExitCheckbox.IsChecked == true;
            App.Settings.TreatNonZeroExitCodeAsFailure = TreatNonZeroExitCodeAsFailureCheckbox.IsChecked == true;
            App.Settings.ForceNorthAmerica = ForceNorthAmericaCheckbox.IsChecked == true;
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Save();
            Close();
        }

        private void ResetCacheButton_OnClick(object sender, RoutedEventArgs e)
        {
            App.UniqueIdCache.Reset();
        }
    }
}