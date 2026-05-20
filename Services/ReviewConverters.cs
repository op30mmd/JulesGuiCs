using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace JulesClient.Services;

public class OriginatorToMaxWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && string.Equals(s, "review", StringComparison.OrdinalIgnoreCase)) return double.NaN;
        return 700.0;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToBorderThicknessConverter : IValueConverter
{
    private static readonly Thickness _one = new(1);
    private static readonly Thickness _zero = new(0);
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && string.Equals(s, "review", StringComparison.OrdinalIgnoreCase)) return _one;
        return _zero;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
