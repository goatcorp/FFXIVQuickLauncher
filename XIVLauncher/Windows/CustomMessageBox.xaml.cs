using System;
using System.Diagnostics;
using System.Media;
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
        private CustomMessageBox()
        {
            InitializeComponent();

            DiscordButton.Click += Util.OpenDiscord;
            DataContext = new ErrorWindowViewModel();

            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        public static void Show(string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Asterisk, bool showHelpLinks = true)
        {
            var box = new CustomMessageBox {MessageTextBlock = {Text = text}, Title = caption};

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

            box.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FaqButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start($"{App.RepoUrl}/wiki/FAQ");
        }
    }
}
