using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

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
            var dlg = new OpenFolderDialog();
            var parent = Window.GetWindow(this);

            dlg.Multiselect = false;
            dlg.Title = this.Description;
            dlg.InitialDirectory = this.Text;
            dlg.ValidateNames = true;

            if (dlg.ShowDialog(parent) == true)
            {
                Text = dlg.FolderName;
                var be = GetBindingExpression(TextProperty);
                be?.UpdateSource();
            }
        }

        private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            TextChanged?.Invoke(sender, e);
        }
    }
}
