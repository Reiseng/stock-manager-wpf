using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StockControl.Utils
{
    // Shows an "empty list" message: 0 => Visible, anything else => Collapsed
    public class ZeroToVisibleConverter : IValueConverter
    {
        public static readonly ZeroToVisibleConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
