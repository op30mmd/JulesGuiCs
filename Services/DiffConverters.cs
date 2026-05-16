using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace JulesClient.Services;

public class DiffLineTypeToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush _addedBrush = new(ColorHelper.FromArgb(40, 0, 255, 0));
    private static readonly SolidColorBrush _removedBrush = new(ColorHelper.FromArgb(40, 255, 0, 0));
    private static readonly SolidColorBrush _metadataBrush = new(ColorHelper.FromArgb(20, 0, 0, 255));
    private static readonly SolidColorBrush _fileHeaderBrush = new(ColorHelper.FromArgb(60, 100, 100, 100));
    private static readonly SolidColorBrush _hunkHeaderBrush = new(ColorHelper.FromArgb(30, 100, 100, 100));
    private static readonly SolidColorBrush _transparentBrush = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => _addedBrush,
                DiffLineType.Removed => _removedBrush,
                DiffLineType.Metadata => _metadataBrush,
                DiffLineType.FileHeader => _fileHeaderBrush,
                DiffLineType.HunkHeader => _hunkHeaderBrush,
                _ => _transparentBrush
            };
        }
        return _transparentBrush;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DiffLineTypeToForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush _addedFg = new(Colors.LightGreen);
    private static readonly SolidColorBrush _removedFg = new(Colors.LightPink);
    private static readonly SolidColorBrush _metadataFg = new(Colors.LightBlue);
    private static SolidColorBrush? _defaultFg;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => _addedFg,
                DiffLineType.Removed => _removedFg,
                DiffLineType.Metadata => _metadataFg,
                _ => GetDefaultForeground()
            };
        }
        return GetDefaultForeground();
    }

    private static Brush GetDefaultForeground()
    {
        if (_defaultFg != null) return _defaultFg;

        if (Application.Current.Resources.TryGetValue("TextControlForeground", out var brush) && brush is Brush b)
        {
            _defaultFg = new SolidColorBrush(((SolidColorBrush)b).Color);
            return _defaultFg;
        }
        _defaultFg = new SolidColorBrush(Colors.White);
        return _defaultFg;
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

public class BoolToChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (bool)value ? "\uE70E" : "\uE76C";
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

