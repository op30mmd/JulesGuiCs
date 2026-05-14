using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using System.Diagnostics;

namespace JulesClient.Services;

public class Base64ToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string base64 && !string.IsNullOrEmpty(base64))
        {
            try
            {
                byte[] bytes = System.Convert.FromBase64String(base64);
                var image = new BitmapImage();
                var ms = new InMemoryRandomAccessStream();
                using (var stream = ms.AsStreamForWrite())
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush();
                }
                ms.Seek(0);
                image.SetSource(ms);
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
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value as string) == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToColorConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        try
        {
            bool isUser = (value as string) == "user";
            string resourceKey = isUser ? "SystemAccentColor" : "SystemControlBackgroundChromeMediumLowBrush";

            if (Application.Current.Resources.TryGetValue(resourceKey, out var res))
            {
                if (res is Brush brush) return brush;
                if (res is Windows.UI.Color color) return new SolidColorBrush(color);
            }

            return isUser ? new SolidColorBrush(Microsoft.UI.Colors.Blue) : new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CONVERTER] OriginatorToColor failed: {ex.Message}");
            return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
