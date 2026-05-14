using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

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
                using (var ms = new InMemoryRandomAccessStream())
                {
                    ms.WriteAsync(bytes.AsBuffer()).AsTask().Wait();
                    ms.Seek(0);
                    image.SetSource(ms);
                }
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
    public object Convert(object value, Type targetType, object parameter, string language) =>
        (value as string) == "user" ?
            App.Current.Resources["SystemAccentColor"] :
            App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"];

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
