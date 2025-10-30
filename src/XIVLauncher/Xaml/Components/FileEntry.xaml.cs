using System.Windows;
using Microsoft.Win32;

namespace XIVLauncher.Xaml.Components
{
    /// <summary>
    /// Interaction logic for FolderEntry.xaml
    /// </summary>
    public partial class FileEntry
    {
        public static DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
            typeof(FileEntry),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static DependencyProperty DescriptionProperty = DependencyProperty.Register("Description",
            typeof(string), typeof(FileEntry), new PropertyMetadata(null));

        public static DependencyProperty FiltersProperty = DependencyProperty.Register("Filters",
            typeof(string), typeof(FileEntry), new PropertyMetadata(null));

        public string Text
        {
            get { return GetValue(TextProperty) as string; }
            set { SetValue(TextProperty, value); }
        }

        public string Description
        {
            get { return GetValue(DescriptionProperty) as string; }
            set { SetValue(DescriptionProperty, value); }
        }

        public string Filters
        {
            get { return GetValue(FiltersProperty) as string; }
            set { SetValue(FiltersProperty, value); }
        }

        public FileEntry()
        {
            InitializeComponent();
        }

        private void BrowseFolder(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            var parent = Window.GetWindow(this);

            dlg.Multiselect = false;
            dlg.Title = Description;
            dlg.Filter = Filters;
            dlg.ValidateNames = true;

            if (dlg.ShowDialog(parent) == true)
            {
                Text = dlg.FileName;
                var be = GetBindingExpression(TextProperty);
                be?.UpdateSource();
            }
        }
    }
}
