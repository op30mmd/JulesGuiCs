using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace JulesClient.Services;

public static class MarkdownParser
{
    private static Brush GetAccentBrush()
    {
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var res))
            {
                if (res is Windows.UI.Color color) return new SolidColorBrush(color);
                if (res is Brush brush) return brush;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MARKDOWN] Failed to get accent color: {ex.Message}");
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Blue);
    }

    public static void ParseInto(TextBlock textBlock, string text)
    {
        try
        {
            textBlock.Inlines.Clear();
            if (string.IsNullOrEmpty(text)) return;

            var accent = GetAccentBrush();
            // Regex to match Bold (**), Italic (*), Inline Code (`), and Links ([text](url))
            var segments = Regex.Split(text, @"(\*\*.*?\*\*|\*.*?\*|`.*?`|\[.*?\]\(.*?\))").Where(s => !string.IsNullOrEmpty(s));

            foreach (var segment in segments)
            {
                if (segment.StartsWith("**") && segment.EndsWith("**"))
                {
                    textBlock.Inlines.Add(new Bold { Inlines = { new Run { Text = segment.Substring(2, segment.Length - 4) } } });
                }
                else if (segment.StartsWith("*") && segment.EndsWith("*"))
                {
                    textBlock.Inlines.Add(new Italic { Inlines = { new Run { Text = segment.Substring(1, segment.Length - 2) } } });
                }
                else if (segment.StartsWith("`") && segment.EndsWith("`"))
                {
                    textBlock.Inlines.Add(new Run
                    {
                        Text = segment.Substring(1, segment.Length - 2),
                        FontFamily = new FontFamily("Consolas"),
                        Foreground = accent,
                        FontSize = textBlock.FontSize - 1
                    });
                }
                else if (segment.StartsWith("[") && segment.Contains("]("))
                {
                    var match = Regex.Match(segment, @"\[(.*?)\]\((.*?)\)");
                    if (match.Success)
                    {
                        var linkText = match.Groups[1].Value;
                        var url = match.Groups[2].Value;
                        try
                        {
                            var hyperlink = new Hyperlink { NavigateUri = new Uri(url) };
                            hyperlink.Inlines.Add(new Run { Text = linkText });
                            ToolTipService.SetToolTip(hyperlink, url);
                            textBlock.Inlines.Add(hyperlink);
                        }
                        catch
                        {
                            textBlock.Inlines.Add(new Run { Text = segment });
                        }
                    }
                    else
                    {
                        textBlock.Inlines.Add(new Run { Text = segment });
                    }
                }
                else
                {
                    textBlock.Inlines.Add(new Run { Text = segment });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MARKDOWN] Parse failed: {ex.Message}");
            textBlock.Text = text; // Fallback to raw text
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
