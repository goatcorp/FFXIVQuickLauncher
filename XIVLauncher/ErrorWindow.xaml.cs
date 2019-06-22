using System;
using System.Diagnostics;
using System.Media;
using System.Windows;

namespace XIVLauncher
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class ErrorWindow : Window
    {
        public ErrorWindow(Exception exc, string message, string context)
        {
            InitializeComponent();

            Serilog.Log.Error(exc, $"ErrorWindow called: [{message}] [{context}]");

            ExceptionTextBox.AppendText(exc.ToString());
            ExceptionTextBox.AppendText("\n" + Util.GetAssemblyVersion());
            ExceptionTextBox.AppendText("\n" + Util.GetGitHash());
            ExceptionTextBox.AppendText("\nContext: " + context);
            ExceptionTextBox.AppendText("\n" + Environment.OSVersion);
            ExceptionTextBox.AppendText("\n" + Environment.Is64BitProcess);
            ExceptionTextBox.AppendText("\n" + Settings.IsDX11());
            ExceptionTextBox.AppendText("\n" + Settings.IsInGameAddonEnabled());
            ExceptionTextBox.AppendText("\n" + Settings.IsAutologin());

            #if DEBUG
            ExceptionTextBox.AppendText("\nDebugging");
            #endif

            ExceptionTextBox.AppendText("\n\n\n" + Properties.Settings.Default.Addons);

            ContextTextBlock.Text = message;

            SystemSounds.Hand.Play();
            BringIntoView();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/goaaats/FFXIVQuickLauncher/issues/new");
        }

        private void FaqButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/goaaats/FFXIVQuickLauncher/wiki/FAQ");
        }

        private void DiscordButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://discord.gg/29NBmud");
        }
    }
}
