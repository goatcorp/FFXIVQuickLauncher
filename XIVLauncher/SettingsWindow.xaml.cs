using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using XIVLauncher;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            GamePathEntry.Text = Settings.GetGamePath();
            Dx11RadioButton.IsChecked = Settings.IsDX11();
            ExpansionLevelComboBox.SelectedIndex = Settings.GetExpansionLevel();
            LanguageComboBox.SelectedIndex = (int) Settings.GetLanguage();

            VersionLabel.Text += " - " + Util.GetGitHash();
        }

        private void SettingsWindow_OnClosing(object sender, CancelEventArgs e)
        {
            Settings.SetGamePath(GamePathEntry.Text);
            Settings.SetDx11(Dx11RadioButton.IsChecked == true);
            Settings.SetExpansionLevel(ExpansionLevelComboBox.SelectedIndex);
            Settings.SetLanguage((ClientLanguage) LanguageComboBox.SelectedIndex);
            Settings.Save();
        }

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/goaaats/FFXIVQuickLauncher");
        }

        private void BackupToolButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(System.IO.Path.Combine(GamePathEntry.Text, "boot", "ffxivconfig.exe"));
        }

        private void OriginalLauncherButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(System.IO.Path.Combine(GamePathEntry.Text, "boot", "ffxivboot.exe"));
        }
    }
}
