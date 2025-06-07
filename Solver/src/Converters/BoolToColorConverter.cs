using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Solver.Converters;

public class BoolToColorConverter : DependencyObject, IValueConverter
{

    public static readonly DependencyProperty TrueValueProperty =
        DependencyProperty.Register(
            "TrueValue",
            typeof(Brush),
            typeof(BoolToColorConverter),
            new PropertyMetadata(Brushes.Black));

    public static readonly DependencyProperty FalseValueProperty =
        DependencyProperty.Register(
            "FalseValue",
            typeof(Brush),
            typeof(BoolToColorConverter),
            new PropertyMetadata(Brushes.Red));

    public Brush TrueValue
    {
        get { return (Brush)GetValue(TrueValueProperty); }
        set { SetValue(TrueValueProperty, value); }
    }

    public Brush FalseValue
    {
        get { return (Brush)GetValue(FalseValueProperty); }
        set { SetValue(FalseValueProperty, value); }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueValue : FalseValue;
        }
        return TrueValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Brush brushValue)
        {
            if (brushValue == TrueValue)
            {
                return true;
            }
            else if (brushValue == FalseValue)
            {
                return false;
            }
        }
        return false;
    }
}
