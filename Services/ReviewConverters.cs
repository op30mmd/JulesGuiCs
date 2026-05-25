using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using FontWeight = Windows.UI.Text.FontWeight;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace JulesClient.Services;

public class OriginatorToMaxWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && string.Equals(s, "review", StringComparison.OrdinalIgnoreCase))
        {
            return double.NaN;
        }
        return 700.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToBorderThicknessConverter : IValueConverter
{
    private static readonly Thickness _two = new(2);
    private static readonly Thickness _zero = new(0);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && string.Equals(s, "review", StringComparison.OrdinalIgnoreCase))
        {
            return _two;
        }
        return _zero;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToBorderBrushConverter : IValueConverter
{
    private static Brush? _accentBrush;
    private static readonly Brush _transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    private static readonly object _lock = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string s || !string.Equals(s, "review", StringComparison.OrdinalIgnoreCase))
        {
            return _transparent;
        }

        if (_accentBrush == null)
        {
            lock (_lock)
            {
                _accentBrush ??= BrushHelper.ResolveBrush("AccentFillColorDefaultBrush", Microsoft.UI.Colors.Blue);
            }
        }

        return _accentBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
