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

// Selects the chat bubble's Style (alignment, background, border, max width) from
// the message originator - one binding instead of five separate converters.
public class OriginatorToBubbleStyleConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        var key = (value as string) switch
        {
            "user" => "UserBubbleStyle",
            "review" => "ReviewBubbleStyle",
            _ => "AgentBubbleStyle"
        };

        try
        {
            if (Application.Current.Resources.TryGetValue(key, out var style) && style is Style s)
                return s;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CONVERTER] OriginatorToBubbleStyle failed: {ex.Message}");
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

// Friendly speaker label for the small caption above a bubble.
public class OriginatorToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value as string) switch
        {
            "user" => "You",
            "agent" => "Jules",
            "review" => "Code Review",
            null or "" => string.Empty,
            var other => char.ToUpperInvariant(other[0]) + other[1..]
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
