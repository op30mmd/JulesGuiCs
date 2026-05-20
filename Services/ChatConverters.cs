using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace JulesClient.Services;

public class Base64ToImageSourceConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _cache = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string base64 && !string.IsNullOrEmpty(base64))
        {
            try
            {
                if (_cache.TryGetValue(base64, out var weakRef) && weakRef.TryGetTarget(out var cached))
                {
                    return cached;
                }

                byte[] bytes = System.Convert.FromBase64String(base64);
                var image = new BitmapImage();
                using var ms = new InMemoryRandomAccessStream();
                using (var stream = ms.AsStreamForWrite())
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
                ms.Seek(0);
                image.SetSource(ms);

                _cache[base64] = new WeakReference<BitmapImage>(image);
                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CONVERTER] Base64ToImageSource failed: {ex.Message}");
                return null;
            }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToAlignmentConverter : IValueConverter
{
    private static readonly object _right = HorizontalAlignment.Right;
    private static readonly object _left = HorizontalAlignment.Left;
    private static readonly object _stretch = HorizontalAlignment.Stretch;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            string? originator = value as string;
            if (string.Equals(originator, "review", StringComparison.OrdinalIgnoreCase)) return _stretch;
            bool isUser = string.Equals(originator, "user", StringComparison.OrdinalIgnoreCase);
            return isUser ? _right : _left;
        }
        catch { return _left; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToColorConverter : IValueConverter
{
    private static Brush? _userBrush;
    private static Brush? _agentBrush;
    private static Brush? _reviewBrush;
    private static readonly object _lock = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            string? originator = value as string;
            if (string.Equals(originator, "review", StringComparison.OrdinalIgnoreCase))
            {
                if (_reviewBrush != null) return _reviewBrush;
                lock (_lock)
                {
                    if (_reviewBrush == null)
                    {
                        _reviewBrush = ResolveBrush("SystemControlBackgroundChromeMediumLowBrush", Microsoft.UI.Colors.LightGray);
                    }
                }
                return _reviewBrush;
            }

            bool isUser = string.Equals(originator, "user", StringComparison.OrdinalIgnoreCase);

            if (isUser)
            {
                if (_userBrush != null) return _userBrush;
                lock (_lock)
                {
                    if (_userBrush == null)
                    {
                        _userBrush = ResolveBrush("SystemAccentColor", Microsoft.UI.Colors.Blue);
                    }
                }
                return _userBrush;
            }
            else
            {
                if (_agentBrush != null) return _agentBrush;
                lock (_lock)
                {
                    if (_agentBrush == null)
                    {
                        _agentBrush = ResolveBrush("SystemControlBackgroundChromeMediumLowBrush", Microsoft.UI.Colors.Gray);
                    }
                }
                return _agentBrush;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CONVERTER] OriginatorToColor failed: {ex.Message}");
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    private static Brush ResolveBrush(string resourceKey, Windows.UI.Color fallback)
    {
        try
        {
            if (Application.Current.Resources.TryGetValue(resourceKey, out var res))
            {
                if (res is Brush brush) return brush;
                if (res is Windows.UI.Color color) return new SolidColorBrush(color);
            }
        }
        catch { }
        return new SolidColorBrush(fallback);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
