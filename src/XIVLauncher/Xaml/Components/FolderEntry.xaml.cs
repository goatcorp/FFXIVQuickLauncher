using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace XIVLauncher.Xaml.Components
{
    /// <summary>
    ///     Interaction logic for FolderEntry.xaml
    /// </summary>
    public partial class FolderEntry
    {
        public static DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
            typeof(FolderEntry),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static DependencyProperty DescriptionProperty = DependencyProperty.Register("Description",
            typeof(string), typeof(FolderEntry), new PropertyMetadata(null));

        public event TextChangedEventHandler TextChanged;

        public string Text
        {
            get => GetValue(TextProperty) as string;
            set => SetValue(TextProperty, value);
        }

        public string Description
        {
            get => GetValue(DescriptionProperty) as string;
            set => SetValue(DescriptionProperty, value);
        }

        public FolderEntry()
        {
            InitializeComponent();
        }

        private void BrowseFolder(object sender, RoutedEventArgs e)
        {
            using (var dlg = new CommonOpenFileDialog())
            {
                dlg.Multiselect = false;
                dlg.IsFolderPicker = true;
                dlg.EnsurePathExists = true;
                dlg.Title = Description;
                var result = dlg.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    Text = dlg.FileName;
                    var be = GetBindingExpression(TextProperty);
                    if (be != null)
                        be.UpdateSource();
                }
            }
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            TextChanged?.Invoke(sender, e);
        }
    }
}