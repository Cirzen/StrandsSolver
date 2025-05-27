using System.Globalization;
using System.Windows.Data;

namespace Solver.Converters
{
    /// <summary>
    /// Converts a <see langword="bool"/> value to its inverse. Feels ridiculously over-engineered, but 
    /// typically used in data binding scenarios to invert a boolean value.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true; // Default to enabled if value is not a bool
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}