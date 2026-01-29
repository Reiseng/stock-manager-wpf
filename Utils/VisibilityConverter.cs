using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace StockControl.Utils
{
    // Converts a null or empty string to Visibility.Visible, otherwise Visibility.Collapsed.
        public class StringNullOrEmptyToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return string.IsNullOrWhiteSpace(value as string)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotImplementedException();
        }
    // Converts a null value to Visibility.Collapsed, otherwise Visibility.Visible.
        public class ModeToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return value?.ToString() == parameter?.ToString()
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotImplementedException();
        }
}