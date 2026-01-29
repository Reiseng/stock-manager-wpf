using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
namespace StockControl.Utils
{
public class EnumDescriptionConverter : IValueConverter
{
    // Converts an enum value to its Description attribute string.
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        var field = value.GetType().GetField(value.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();

        return attribute?.Description ?? value.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

}
