using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace XIVLauncher.Xaml
{
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Brushes.Gray : Util.SolidColorBrushFromArgb((int) value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}