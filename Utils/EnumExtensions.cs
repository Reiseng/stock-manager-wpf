using System.ComponentModel;
using System.Reflection;

namespace StockControl.Utils
{
    public static class EnumExtensions
    {
        // Text of the [Description] attribute of an enum value
        public static string GetDescription(this Enum value)
        {
            if (value == null)
                return string.Empty;

            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();

            return attribute?.Description ?? value.ToString();
        }
    }
}
