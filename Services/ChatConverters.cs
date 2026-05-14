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
                var task = ms.WriteAsync(bytes.AsBuffer()).AsTask();
                task.GetAwaiter().GetResult();
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
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if ((value as string) == "user")
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var accent))
            {
                if (accent is Windows.UI.Color color) return new SolidColorBrush(color);
                if (accent is Brush brush) return brush;
            }
            return new SolidColorBrush(Microsoft.UI.Colors.Blue); // Fallback
        }

        if (Application.Current.Resources.TryGetValue("SystemControlBackgroundChromeMediumLowBrush", out var chrome))
        {
            if (chrome is Brush brush) return brush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Gray); // Fallback
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
