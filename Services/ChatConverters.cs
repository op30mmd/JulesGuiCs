using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;

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
                // We use GetAwaiter().GetResult() as a workaround for synchronous IValueConverter
                ms.WriteAsync(bytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
                ms.Seek(0);
                image.SetSource(ms);
                return image;
            }
            catch { return null; }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value as string) == "user" ? Microsoft.UI.Xaml.HorizontalAlignment.Right : Microsoft.UI.Xaml.HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class OriginatorToColorConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
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

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
