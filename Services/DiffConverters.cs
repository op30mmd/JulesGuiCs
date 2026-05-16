using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;

namespace JulesClient.Services;

public class DiffLineTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => new SolidColorBrush(ColorHelper.FromArgb(40, 0, 255, 0)),
                DiffLineType.Removed => new SolidColorBrush(ColorHelper.FromArgb(40, 255, 0, 0)),
                DiffLineType.Metadata => new SolidColorBrush(ColorHelper.FromArgb(20, 0, 0, 255)),
                DiffLineType.FileHeader => new SolidColorBrush(ColorHelper.FromArgb(60, 100, 100, 100)),
                DiffLineType.HunkHeader => new SolidColorBrush(ColorHelper.FromArgb(30, 100, 100, 100)),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DiffLineTypeToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => new SolidColorBrush(Colors.LightGreen),
                DiffLineType.Removed => new SolidColorBrush(Colors.LightPink),
                DiffLineType.Metadata => new SolidColorBrush(Colors.LightBlue),
                _ => GetDefaultForeground()
            };
        }
        return GetDefaultForeground();
    }

    private static Brush GetDefaultForeground()
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue("TextControlForeground", out var brush) && brush is Brush b)
            return b;
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DiffPrefixConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => "+",
                DiffLineType.Removed => "-",
                DiffLineType.FileHeader => "F",
                DiffLineType.HunkHeader => "H",
                _ => " "
            };
        }
        return " ";
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DiffLineTypeToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type && (type == DiffLineType.FileHeader || type == DiffLineType.HunkHeader))
            return FontWeights.Bold;
        return FontWeights.Normal;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
