using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace JulesClient.Services;

public class DiffLineTypeToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => new SolidColorBrush(ColorHelper.FromArgb(0x1A, 0x23, 0x7A, 0x23)),
                DiffLineType.Removed => new SolidColorBrush(ColorHelper.FromArgb(0x1A, 0x8A, 0x1C, 0x1C)),
                DiffLineType.Metadata => new SolidColorBrush(ColorHelper.FromArgb(0x0D, 0x00, 0x78, 0xD4)),
                DiffLineType.FileHeader => new SolidColorBrush(ColorHelper.FromArgb(0x0D, 0x00, 0x00, 0x00)),
                DiffLineType.HunkHeader => new SolidColorBrush(ColorHelper.FromArgb(0x08, 0x00, 0x00, 0x00)),
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
                DiffLineType.Added => new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6E, 0xC0, 0x6E)),
                DiffLineType.Removed => new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0x7E, 0x7E)),
                DiffLineType.Metadata => new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x7E, 0xC8, 0xE8)),
                _ => GetDefaultForeground()
            };
        }
        return GetDefaultForeground();
    }

    private static Brush GetDefaultForeground()
    {
        if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out var brush) && brush is Brush b)
            return b;
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DiffPrefixConverter : IValueConverter
{
    private const string _addedPrefix = "+";
    private const string _removedPrefix = "-";
    private const string _fileHeaderPrefix = "F";
    private const string _hunkHeaderPrefix = "H";
    private const string _contextPrefix = " ";

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => _addedPrefix,
                DiffLineType.Removed => _removedPrefix,
                DiffLineType.FileHeader => _fileHeaderPrefix,
                DiffLineType.HunkHeader => _hunkHeaderPrefix,
                _ => _contextPrefix
            };
        }
        return _contextPrefix;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DiffLineTypeToFontWeightConverter : IValueConverter
{
    private static readonly FontWeight _bold = new(700);
    private static readonly FontWeight _semiBold = new(600);
    private static readonly FontWeight _normal = new(400);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.FileHeader => _bold,
                DiffLineType.HunkHeader => _semiBold,
                _ => _normal
            };
        }
        return _normal;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
