using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;

namespace JulesClient.Services;

public class DiffToParsedPatchConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string patch)
        {
            return DiffParser.Parse(patch);
        }
        return null!;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
