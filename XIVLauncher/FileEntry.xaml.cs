using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace XIVLauncher
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
            using (var dlg = new CommonOpenFileDialog())
            {
                dlg.Multiselect = false;
                dlg.IsFolderPicker = false;
                dlg.EnsurePathExists = true;
                dlg.Title = Description;

                var filterSets = Filters.Split(';');

                foreach (var filterSet in filterSets)
                {
                    var filterOptions = filterSet.Split(',');
                    dlg.Filters.Add(new CommonFileDialogFilter(filterOptions[0], filterOptions[1]));
                }
                
                var result = dlg.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    Text = dlg.FileName;
                    BindingExpression be = GetBindingExpression(TextProperty);
                    if (be != null)
                        be.UpdateSource();
                }
            }
        }
    }
}
