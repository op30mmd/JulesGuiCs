using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System.Text.RegularExpressions;

namespace JulesClient.Services;

public static class MarkdownParser
{
    public static void ParseInto(TextBlock textBlock, string text)
    {
        textBlock.Inlines.Clear();
        if (string.IsNullOrEmpty(text)) return;

        // Simple regex-based parsing for:
        // `code` -> Inline Code
        // **bold** -> Bold
        // *italic* -> Italic

        var segments = Regex.Split(text, @"(\*\*.*?\*\*|\*.*?\*|`.*?`)").Where(s => !string.IsNullOrEmpty(s));

        foreach (var segment in segments)
        {
            if (segment.StartsWith("**") && segment.EndsWith("**"))
            {
                textBlock.Inlines.Add(new Bold { Inlines = { new Run { Text = segment.Trim('*') } } });
            }
            else if (segment.StartsWith("*") && segment.EndsWith("*"))
            {
                textBlock.Inlines.Add(new Italic { Inlines = { new Run { Text = segment.Trim('*') } } });
            }
            else if (segment.StartsWith("`") && segment.EndsWith("`"))
            {
                textBlock.Inlines.Add(new Run
                {
                    Text = segment.Trim('`'),
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemAccentColor"]
                });
            }
            else
            {
                textBlock.Inlines.Add(new Run { Text = segment });
            }
        }
    }
}

public class MarkdownHelper
{
    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(MarkdownHelper), new PropertyMetadata(null, OnTextChanged));

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock tb && e.NewValue is string text)
        {
            MarkdownParser.ParseInto(tb, text);
        }
    }
}
