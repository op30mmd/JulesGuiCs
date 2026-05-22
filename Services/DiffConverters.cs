using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using FontWeights = Microsoft.UI.Text.FontWeights;

namespace JulesClient.Services;

public class DiffLineTypeToColorConverter : IValueConverter
{
    private static SolidColorBrush? _addedBg;
    private static SolidColorBrush? _removedBg;
    private static SolidColorBrush? _metadataBg;
    private static SolidColorBrush? _fileHeaderBg;
    private static SolidColorBrush? _hunkHeaderBg;
    private static readonly SolidColorBrush _transparent = new(Colors.Transparent);

    private static SolidColorBrush GetAddedBg() => _addedBg ??= new SolidColorBrush(ColorHelper.FromArgb(0x1A, 0x23, 0x7A, 0x23));
    private static SolidColorBrush GetRemovedBg() => _removedBg ??= new SolidColorBrush(ColorHelper.FromArgb(0x1A, 0x8A, 0x1C, 0x1C));
    private static SolidColorBrush GetMetadataBg() => _metadataBg ??= new SolidColorBrush(ColorHelper.FromArgb(0x0D, 0x00, 0x78, 0xD4));
    private static SolidColorBrush GetFileHeaderBg() => _fileHeaderBg ??= new SolidColorBrush(ColorHelper.FromArgb(0x0D, 0x00, 0x00, 0x00));
    private static SolidColorBrush GetHunkHeaderBg() => _hunkHeaderBg ??= new SolidColorBrush(ColorHelper.FromArgb(0x08, 0x00, 0x00, 0x00));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => GetAddedBg(),
                DiffLineType.Removed => GetRemovedBg(),
                DiffLineType.Metadata => GetMetadataBg(),
                DiffLineType.FileHeader => GetFileHeaderBg(),
                DiffLineType.HunkHeader => GetHunkHeaderBg(),
                _ => _transparent
            };
        }
        return _transparent;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class DiffLineTypeToForegroundConverter : IValueConverter
{
    private static SolidColorBrush? _addedFg;
    private static SolidColorBrush? _removedFg;
    private static SolidColorBrush? _metadataFg;
    private static Brush? _defaultFg;

    private static SolidColorBrush GetAddedFg() => _addedFg ??= new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6E, 0xC0, 0x6E));
    private static SolidColorBrush GetRemovedFg() => _removedFg ??= new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0x7E, 0x7E));
    private static SolidColorBrush GetMetadataFg() => _metadataFg ??= new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x7E, 0xC8, 0xE8));

    private static Brush GetDefaultFg()
    {
        if (_defaultFg != null) return _defaultFg;
        if (Application.Current.Resources.TryGetValue("TextFillColorPrimaryBrush", out var brush) && brush is Brush b)
            return _defaultFg = b;
        return _defaultFg = new SolidColorBrush(Colors.White);
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => GetAddedFg(),
                DiffLineType.Removed => GetRemovedFg(),
                DiffLineType.Metadata => GetMetadataFg(),
                _ => GetDefaultFg()
            };
        }
        return GetDefaultFg();
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
    private static readonly FontWeight _bold = FontWeights.Bold;
    private static readonly FontWeight _semiBold = FontWeights.SemiBold;
    private static readonly FontWeight _normal = FontWeights.Normal;

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
