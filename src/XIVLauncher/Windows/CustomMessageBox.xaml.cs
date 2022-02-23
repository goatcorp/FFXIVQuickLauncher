using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using XIVLauncher.Common;
using XIVLauncher.Common.Game;
using XIVLauncher.Support;
using XIVLauncher.Windows.ViewModel;
using XIVLauncher.Xaml;

namespace XIVLauncher.Windows
{
    /// <summary>
    /// Interaction logic for ErrorWindow.xaml
    /// </summary>
    public partial class CustomMessageBox : Window
    {
        private readonly Builder _builder;
        private MessageBoxResult _result;

        private ErrorWindowViewModel ViewModel => DataContext as ErrorWindowViewModel;

        private CustomMessageBox(Builder builder)
        {
            _builder = builder;
            _result = _builder.CancelResult;

            InitializeComponent();

            DataContext = new ErrorWindowViewModel();

            OfficialLauncherButton.Click += (_, _) =>
            {
                if (MessageBox.Show("Steam account?", "XIVLauncher", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    Process.Start($"steam://rungameid/{Launcher.STEAM_APP_ID}");
                else
                    Util.StartOfficialLauncher(App.Settings.GamePath, false);
            };
            ViewModel.CopyMessageTextCommand = new AsyncCommand(p => Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(_builder.Text)));

            DiscordButton.Click += SupportLinks.OpenDiscord;
            FaqButton.Click += SupportLinks.OpenFaq;

            Title = builder.Caption;
            MessageTextBlock.Text = builder.Text;
            switch (builder.Buttons)
            {
                case MessageBoxButton.OK:
                    Button1.Content = builder.OkButtonText ?? ViewModel.OkLoc;
                    Button2.Visibility = Visibility.Collapsed;
                    Button3.Visibility = Visibility.Collapsed;
                    (builder.DefaultResult switch
                    {
                        MessageBoxResult.OK => Button1,
                        _ => throw new ArgumentOutOfRangeException(nameof(builder.DefaultResult), builder.DefaultResult, null),
                    }).Focus();
                    break;
                case MessageBoxButton.OKCancel:
                    Button1.Content = builder.OkButtonText ?? ViewModel.OkLoc;
                    Button2.Content = builder.CancelButtonText ?? ViewModel.CancelWithShortcutLoc;
                    Button3.Visibility = Visibility.Collapsed;
                    (builder.DefaultResult switch
                    {
                        MessageBoxResult.OK => Button1,
                        MessageBoxResult.Cancel => Button2,
                        _ => throw new ArgumentOutOfRangeException(nameof(builder.DefaultResult), builder.DefaultResult, null),
                    }).Focus();
                    break;
                case MessageBoxButton.YesNoCancel:
                    Button1.Content = builder.YesButtonText ?? ViewModel.YesWithShortcutLoc;
                    Button2.Content = builder.NoButtonText ?? ViewModel.NoWithShortcutLoc;
                    Button3.Content = builder.CancelButtonText ?? ViewModel.CancelWithShortcutLoc;
                    (builder.DefaultResult switch
                    {
                        MessageBoxResult.Yes => Button1,
                        MessageBoxResult.No => Button2,
                        MessageBoxResult.Cancel => Button3,
                        _ => throw new ArgumentOutOfRangeException(nameof(builder.DefaultResult), builder.DefaultResult, null),
                    }).Focus();
                    break;
                case MessageBoxButton.YesNo:
                    Button1.Content = builder.YesButtonText ?? ViewModel.YesWithShortcutLoc;
                    Button2.Content = builder.NoButtonText ?? ViewModel.NoWithShortcutLoc;
                    Button3.Visibility = Visibility.Collapsed;
                    (builder.DefaultResult switch
                    {
                        MessageBoxResult.Yes => Button1,
                        MessageBoxResult.No => Button2,
                        _ => throw new ArgumentOutOfRangeException(nameof(builder.DefaultResult), builder.DefaultResult, null),
                    }).Focus();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(builder.Buttons), builder.Buttons, null);
            }

            switch (builder.Image)
            {
                case MessageBoxImage.None:
                    ErrorPackIcon.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxImage.Hand:
                    ErrorPackIcon.Visibility = Visibility.Visible;
                    ErrorPackIcon.Kind = PackIconKind.Error;
                    ErrorPackIcon.Foreground = Brushes.Red;
                    SystemSounds.Hand.Play();
                    break;
                case MessageBoxImage.Question:
                    ErrorPackIcon.Visibility = Visibility.Visible;
                    ErrorPackIcon.Kind = PackIconKind.QuestionMarkCircle;
                    ErrorPackIcon.Foreground = Brushes.DodgerBlue;
                    SystemSounds.Question.Play();
                    break;
                case MessageBoxImage.Exclamation:
                    ErrorPackIcon.Visibility = Visibility.Visible;
                    ErrorPackIcon.Kind = PackIconKind.Warning;
                    ErrorPackIcon.Foreground = Brushes.Yellow;
                    SystemSounds.Exclamation.Play();
                    break;
                case MessageBoxImage.Asterisk:
                    ErrorPackIcon.Visibility = Visibility.Visible;
                    ErrorPackIcon.Kind = PackIconKind.Information;
                    ErrorPackIcon.Foreground = Brushes.DodgerBlue;
                    SystemSounds.Asterisk.Play();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(builder.Image), builder.Image, null);
            }

            OfficialLauncherButton.Visibility = builder.ShowOfficialLauncher ? Visibility.Visible : Visibility.Collapsed;
            DiscordButton.Visibility = builder.ShowDiscordLink ? Visibility.Visible : Visibility.Collapsed;
            FaqButton.Visibility = builder.ShowHelpLinks ? Visibility.Visible : Visibility.Collapsed;
            IntegrityReportButton.Visibility = builder.ShowReportLinks ? Visibility.Visible : Visibility.Collapsed;

            Topmost = builder.TopMost;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();

            base.OnKeyDown(e);
        }

        private void CustomMessageBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            _result = _builder.Buttons switch
            {
                MessageBoxButton.OK => MessageBoxResult.OK,
                MessageBoxButton.OKCancel => MessageBoxResult.OK,
                MessageBoxButton.YesNoCancel => MessageBoxResult.Yes,
                MessageBoxButton.YesNo => MessageBoxResult.Yes,
                _ => throw new NotImplementedException(),
            };
            Close();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            _result = _builder.Buttons switch
            {
                MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
                MessageBoxButton.YesNoCancel => MessageBoxResult.No,
                MessageBoxButton.YesNo => MessageBoxResult.No,
                _ => throw new NotImplementedException(),
            };
            Close();
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            _result = _builder.Buttons switch
            {
                MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
                _ => throw new NotImplementedException(),
            };
            Close();
        }

        private void IntegrityReportButton_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start(Path.Combine(Paths.RoamingPath, "integrityreport.txt"));
        }

        public class Builder
        {
            internal string Text;
            internal string Caption = "XIVLauncher";
            internal MessageBoxButton Buttons = MessageBoxButton.OK;
            internal MessageBoxResult DefaultResult = MessageBoxResult.None;  // On enter
            internal MessageBoxResult CancelResult = MessageBoxResult.None;  // On escape
            internal MessageBoxImage Image = MessageBoxImage.None;
            internal string OkButtonText;
            internal string CancelButtonText;
            internal string YesButtonText;
            internal string NoButtonText;
            internal bool TopMost = false;
            internal bool ShowHelpLinks = false;
            internal bool ShowDiscordLink = false;
            internal bool ShowReportLinks = false;
            internal bool ShowOfficialLauncher = false;

            public Builder() { }
            public Builder WithText(string text) { Text = text; return this; }
            public Builder WithCaption(string caption) { Caption = caption; return this; }
            public Builder WithButtons(MessageBoxButton buttons) { Buttons = buttons; return this; }
            public Builder WithDefaultResult(MessageBoxResult result) { DefaultResult = result; return this; }
            public Builder WithCancelResult(MessageBoxResult result) { CancelResult = result; return this; }
            public Builder WithImage(MessageBoxImage image) { Image = image; return this; }
            public Builder WithTopMost(bool topMost = true) { TopMost = topMost; return this; }
            public Builder WithOkButtonText(string text) { OkButtonText = text; return this; }
            public Builder WithCancelButtonText(string text) { CancelButtonText = text; return this; }
            public Builder WithYesButtonText(string text) { YesButtonText = text; return this; }
            public Builder WithNoButtonText(string text) { NoButtonText = text; return this; }
            public Builder WithShowHelpLinks(bool showHelpLinks) { ShowHelpLinks = showHelpLinks; return this; }
            public Builder WithShowDiscordLink(bool showDiscordLink) { ShowDiscordLink = showDiscordLink; return this; }
            public Builder WithShowOfficialLauncher(bool showOfficialLauncher) { ShowOfficialLauncher = showOfficialLauncher; return this; }
            public Builder WithShowReportLink(bool showReportLinks) { ShowReportLinks = showReportLinks; return this; }

            public MessageBoxResult ShowAssumingDispatcherThread()
            {
                DefaultResult = DefaultResult != MessageBoxResult.None ? DefaultResult : Buttons switch
                {
                    MessageBoxButton.OK => MessageBoxResult.OK,
                    MessageBoxButton.OKCancel => MessageBoxResult.OK,
                    MessageBoxButton.YesNoCancel => MessageBoxResult.Yes,
                    MessageBoxButton.YesNo => MessageBoxResult.Yes,
                    _ => throw new NotImplementedException(),
                };

                CancelResult = CancelResult != MessageBoxResult.None ? CancelResult : Buttons switch
                {
                    MessageBoxButton.OK => MessageBoxResult.OK,
                    MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
                    MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
                    MessageBoxButton.YesNo => MessageBoxResult.No,
                    _ => throw new NotImplementedException(),
                };

                var res = new CustomMessageBox(this);
                res.ShowDialog();
                return res._result;
            }

            public MessageBoxResult ShowInNewThread()
            {
                MessageBoxResult? res = null;
                var newWindowThread = new Thread(() => res = ShowAssumingDispatcherThread());
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.IsBackground = true;
                newWindowThread.Start();
                newWindowThread.Join();
                return res.GetValueOrDefault(CancelResult);
            }

            public MessageBoxResult Show()
            {
                if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                    return ShowAssumingDispatcherThread();
                else
                    return Application.Current.Dispatcher.Invoke(ShowAssumingDispatcherThread);
            }
        }

        public static MessageBoxResult Show(string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Asterisk, bool showHelpLinks = true, bool showDiscordLink = true, bool showReportLinks = false, bool showOfficialLauncher = false)
        {
            return new Builder()
                .WithCaption(caption)
                .WithText(text)
                .WithTopMost(true)
                .WithButtons(buttons)
                .WithImage(image)
                .WithShowHelpLinks(showHelpLinks)
                .WithShowDiscordLink(showDiscordLink)
                .WithShowReportLink(showReportLinks)
                .WithShowOfficialLauncher(showOfficialLauncher)
                .Show();
        }
    }
}