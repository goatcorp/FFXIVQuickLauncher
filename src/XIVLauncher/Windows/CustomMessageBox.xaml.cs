using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using XIVLauncher.Settings;
using XIVLauncher.Windows.ViewModel;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        public static App InvokableApp { get; set; } 

        private CustomMessageBox()
        {
            InitializeComponent();

            DiscordButton.Click += Util.OpenDiscord;
            FaqButton.Click += Util.OpenFaq;
            DataContext = new ErrorWindowViewModel();

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        public static void Show(string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Asterisk, bool showHelpLinks = true, bool showReportLinks = false)
        {
            var signal = new ManualResetEvent(false);

            InvokableApp.Dispatcher.Invoke(() =>
            {
                var box = new CustomMessageBox { MessageTextBlock = { Text = text }, Title = caption };

                switch (image)
                {
                    case MessageBoxImage.None:
                        box.ErrorPackIcon.Visibility = Visibility.Collapsed;
                        break;
                    case MessageBoxImage.Hand:
                        box.ErrorPackIcon.Visibility = Visibility.Visible;
                        box.ErrorPackIcon.Kind = PackIconKind.Error;
                        box.ErrorPackIcon.Foreground = Brushes.Red;
                        SystemSounds.Hand.Play();
                        break;
                    case MessageBoxImage.Question:
                        box.ErrorPackIcon.Visibility = Visibility.Visible;
                        box.ErrorPackIcon.Kind = PackIconKind.QuestionMarkCircle;
                        box.ErrorPackIcon.Foreground = Brushes.DodgerBlue;
                        SystemSounds.Question.Play();
                        break;
                    case MessageBoxImage.Exclamation:
                        box.ErrorPackIcon.Visibility = Visibility.Visible;
                        box.ErrorPackIcon.Kind = PackIconKind.Warning;
                        box.ErrorPackIcon.Foreground = Brushes.Yellow;
                        SystemSounds.Exclamation.Play();
                        break;
                    case MessageBoxImage.Asterisk:
                        box.ErrorPackIcon.Visibility = Visibility.Visible;
                        box.ErrorPackIcon.Kind = PackIconKind.Information;
                        box.ErrorPackIcon.Foreground = Brushes.DodgerBlue;
                        SystemSounds.Asterisk.Play();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(image), image, null);
                }

                if (!showHelpLinks)
                {
                    box.DiscordButton.Visibility = Visibility.Collapsed;
                    box.FaqButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    box.DiscordButton.Visibility = Visibility.Visible;
                    box.FaqButton.Visibility = Visibility.Visible;
                }

                if(!showReportLinks)
                {
                    box.IntegrityReportButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    box.IntegrityReportButton.Visibility = Visibility.Visible;
                }

                box.ShowDialog();

                signal.Set();
            });

            signal.WaitOne();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

         private void IntegrityReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(Path.Combine(Paths.RoamingPath, "integrityreport.txt"));
        }

    }
}
