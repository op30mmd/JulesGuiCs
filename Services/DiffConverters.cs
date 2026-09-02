using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace JulesClient.Services;

public class DiffLineTypeToColorConverter : IValueConverter
{
    private static SolidColorBrush? _addedBg;
    private static SolidColorBrush? _removedBg;
    private static readonly SolidColorBrush _transparent = new(Colors.Transparent);

    // Row tint - kept low-alpha so the coloured gutter sign carries the emphasis.
    private static SolidColorBrush GetAddedBg() => _addedBg ??= new SolidColorBrush(ColorHelper.FromArgb(0x22, 0x2E, 0xA0, 0x43));
    private static SolidColorBrush GetRemovedBg() => _removedBg ??= new SolidColorBrush(ColorHelper.FromArgb(0x22, 0xD1, 0x3A, 0x3A));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DiffLineType type)
        {
            return type switch
            {
                DiffLineType.Added => GetAddedBg(),
                DiffLineType.Removed => GetRemovedBg(),
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

