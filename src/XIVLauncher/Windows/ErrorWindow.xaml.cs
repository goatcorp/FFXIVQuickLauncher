using System;
using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Documents;
using XIVLauncher.Settings;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
        public ErrorWindow(Exception exc, string message, string context)
        {
            InitializeComponent();

            DiscordButton.Click += Util.OpenDiscord;
            FaqButton.Click += Util.OpenFaq;
            DataContext = new ErrorWindowViewModel();

            ExceptionTextBox.AppendText(exc.ToString());
            ExceptionTextBox.AppendText("\nVersion: " + Util.GetAssemblyVersion());
            ExceptionTextBox.AppendText("\nGit Hash: " + Util.GetGitHash());
            ExceptionTextBox.AppendText("\nContext: " + context);
            ExceptionTextBox.AppendText("\nOS: " + Environment.OSVersion);
            ExceptionTextBox.AppendText("\n64bit? " + Environment.Is64BitProcess);

            if (App.Settings != null)
            {
                ExceptionTextBox.AppendText("\nDX11? " + App.Settings.IsDx11);
                ExceptionTextBox.AppendText("\nAddons Enabled? " + App.Settings.InGameAddonEnabled);
                ExceptionTextBox.AppendText("\nAuto Login Enabled? " + App.Settings.AutologinEnabled);
                ExceptionTextBox.AppendText("\nLanguage: " + App.Settings.Language);
                ExceptionTextBox.AppendText("\nLauncherLanguage: " + App.Settings.LauncherLanguage);
                ExceptionTextBox.AppendText("\nGame path: " + App.Settings.GamePath);

                // When this happens we probably don't want them to run into it again, in case it's an issue with a moved game for example
                App.Settings.AutologinEnabled = false;
            }

#if DEBUG
            ExceptionTextBox.AppendText("\nDebugging");
#endif

            ContextTextBlock.Text = message;

            SystemSounds.Hand.Play();

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start($"{App.RepoUrl}/issues/new");
        }
    }
}
