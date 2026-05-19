using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System.Diagnostics;
using System.Text;

namespace JulesClient.Services;

internal static class MdStyles
{
    public static Windows.UI.Text.FontWeight Bold => new() { Weight = 700 };
    public static Windows.UI.Text.FontWeight SemiBold => new() { Weight = 600 };
    public static Windows.UI.Text.FontWeight Normal => new() { Weight = 400 };
}

public static class MarkdownParser
{
    private static readonly char[] _newlineChars = new[] { '\r', '\n' };

    public static void ParseInto(TextBlock textBlock, string text)
    {
        try
        {
            textBlock.Inlines.Clear();
            if (string.IsNullOrEmpty(text)) return;

            var lines = text.Split(_newlineChars);
            var i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (IsBlank(line))
                {
                    i++;
                    continue;
                }

                if (TryParseCodeBlock(lines, ref i, textBlock)) continue;
                if (TryParseHeading(line, textBlock)) { i++; continue; }
                if (TryParseHorizontalRule(line, textBlock)) { i++; continue; }
                if (TryParseBlockquote(lines, ref i, textBlock)) continue;
                if (TryParseUnorderedList(lines, ref i, textBlock)) continue;
                if (TryParseOrderedList(lines, ref i, textBlock)) continue;
                if (TryParseTable(lines, ref i, textBlock)) continue;
                if (TryParseImage(line, textBlock)) { i++; continue; }

                ParseInlineLine(line, textBlock, addNewline: true);
                i++;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MARKDOWN] Parse failed: {ex.Message}");
            textBlock.Text = text;
        }
    }

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static bool TryParseCodeBlock(string[] lines, ref int index, TextBlock textBlock)
    {
        var line = lines[index];
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("```")) return false;

        var fenceLen = trimmed.TakeWhile(c => c == '`').Count();
        if (fenceLen < 3) return false;

        var lang = trimmed.Substring(fenceLen).Trim();
        var sb = new StringBuilder();
        index++;

        while (index < lines.Length)
        {
            var current = lines[index].TrimEnd();
            if (current.TrimStart().StartsWith("```") && current.TrimStart().TakeWhile(c => c == '`').Count() >= fenceLen)
            {
                index++;
                break;
            }
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(lines[index]);
            index++;
        }

        AddCodeBlock(textBlock, sb.ToString(), lang);
        return true;
    }

    private static bool TryParseHeading(string line, TextBlock textBlock)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '#') return false;

        var level = trimmed.TakeWhile(c => c == '#').Count();
        if (level > 6 || level < 1) return false;

        var content = trimmed.Substring(level).Trim();
        if (content.Length == 0) return false;

        if (content.StartsWith(" ")) content = content.Substring(1);
        if (content.EndsWith("#")) content = content.TrimEnd('#').TrimEnd();

        double fontSize = level switch
        {
            1 => 28,
            2 => 24,
            3 => 20,
            4 => 18,
            5 => 16,
            _ => 14
        };

        var weight = level <= 3 ? MdStyles.Bold : MdStyles.SemiBold;

        var span = CreateInlineSpan(textBlock, content);
        foreach (var inline in span.Inlines)
        {
            if (inline is Run run)
            {
                run.FontSize = fontSize;
                run.FontWeight = weight;
            }
            textBlock.Inlines.Add(inline);
        }
        textBlock.Inlines.Add(new LineBreak());
        return true;
    }

    private static bool TryParseHorizontalRule(string line, TextBlock textBlock)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3) return false;

        char marker = trimmed[0];
        if (marker != '-' && marker != '*' && marker != '_') return false;

        var count = trimmed.TakeWhile(c => c == marker || c == ' ').Count(c => c == marker);
        if (count < 3) return false;

        var rect = new Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Margin = new Thickness(0, 8, 0, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var container = new InlineUIContainer { Child = rect };
        textBlock.Inlines.Add(container);
        textBlock.Inlines.Add(new LineBreak());
        return true;
    }

    private static bool TryParseBlockquote(string[] lines, ref int index, TextBlock textBlock)
    {
        var line = lines[index];
        if (!line.TrimStart().StartsWith(">")) return false;

        var sb = new StringBuilder();
        while (index < lines.Length)
        {
            var current = lines[index].TrimStart();
            if (!current.StartsWith(">")) break;
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(current.Substring(1).TrimStart());
            index++;
        }

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(8, 2, 4, 2),
            Margin = new Thickness(0, 2, 0, 2),
            Child = new TextBlock { Text = sb.ToString(), TextWrapping = TextWrapping.Wrap, Opacity = 0.8, FontSize = 13 }
        };

        var container = new InlineUIContainer { Child = border };
        textBlock.Inlines.Add(container);
        textBlock.Inlines.Add(new LineBreak());
        return true;
    }

    private static bool TryParseUnorderedList(string[] lines, ref int index, TextBlock textBlock)
    {
        var line = lines[index];
        if (!IsUnorderedListItem(line)) return false;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12, 2, 0, 2) };

        while (index < lines.Length)
        {
            var current = lines[index];
            if (!IsUnorderedListItem(current)) break;

            var content = ExtractListItemContent(current);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            row.Children.Add(new TextBlock { Text = "\u2022", Margin = new Thickness(0, 0, 6, 0), FontWeight = MdStyles.Bold });
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };
            ParseInto(tb, content);
            row.Children.Add(tb);
            panel.Children.Add(row);
            index++;
        }

        var container = new InlineUIContainer { Child = panel };
        textBlock.Inlines.Add(container);
        textBlock.Inlines.Add(new LineBreak());
        return true;
    }

    private static bool IsUnorderedListItem(string line)
    {
        var trimmed = line.TrimStart();
        return (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ ")) && trimmed.Length > 2;
    }

    private static string ExtractListItemContent(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Substring(2).Trim();
    }

    private static bool TryParseOrderedList(string[] lines, ref int index, TextBlock textBlock)
    {
        var line = lines[index];
        if (!IsOrderedListItem(line)) return false;

        var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(12, 2, 0, 2) };
        int itemNum = 1;

        while (index < lines.Length)
        {
            var current = lines[index];
            if (!IsOrderedListItem(current)) break;

            var content = ExtractOrderedItemContent(current);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            row.Children.Add(new TextBlock { Text = $"{itemNum}.", Margin = new Thickness(0, 0, 6, 0), FontWeight = MdStyles.SemiBold });
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };
            ParseInto(tb, content);
            row.Children.Add(tb);
            panel.Children.Add(row);
            itemNum++;
            index++;
        }

        var container = new InlineUIContainer { Child = panel };
        textBlock.Inlines.Add(container);
        textBlock.Inlines.Add(new LineBreak());
        return true;
    }

    private static bool IsOrderedListItem(string line)
    {
        var trimmed = line.TrimStart();
        var dotIdx = trimmed.IndexOf(". ");
        return dotIdx > 0 && int.TryParse(trimmed.Substring(0, dotIdx), out _);
    }

    private static string ExtractOrderedItemContent(string line)
    {
        var trimmed = line.TrimStart();
        var dotIdx = trimmed.IndexOf(". ");
        return trimmed.Substring(dotIdx + 2).Trim();
    }

    private static bool TryParseTable(string[] lines, ref int index, TextBlock textBlock)
    {
        var line = lines[index];
        if (!line.Contains('|')) return false;

        var rows = new List<string[]>();
        var headerParts = ParseTableRow(line);
        if (headerParts.Length < 2) return false;

        rows.Add(headerParts);

        index++;
        if (index >= lines.Length) return false;

        var sepLine = lines[index].Trim();
        if (!sepLine.Contains('|') || !sepLine.Contains('-')) return false;
        index++;

        while (index < lines.Length)
        {
            var current = lines[index].Trim();
            if (!current.Contains('|')) break;
            var parts = ParseTableRow(current);
            if (parts.Length != headerParts.Length) break;
            rows.Add(parts);
            index++;
        }

        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        var colCount = headerParts.Length;

        for (int c = 0; c < colCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (int r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int c = 0; c < colCount; c++)
            {
                var cellContent = c < rows[r].Length ? rows[r][c].Trim() : "";
                var cellBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(6, 3, 6, 3),
                    Background = r == 0 ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(30, 128, 128, 128)) : null
                };

                var cellTb = new TextBlock { Text = cellContent, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
                if (r == 0) cellTb.FontWeight = MdStyles.Bold;
                cellBorder.Child = cellTb;

                Grid.SetRow(cellBorder, r);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        var container = new InlineUIContainer { Child = grid };
        textBlock.Inlines.Add(container);
        textBlock.Inlines.Add(new LineBreak());
        return true;
    }

    private static string[] ParseTableRow(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
        if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);
        return trimmed.Split('|');
    }

    private static bool TryParseImage(string line, TextBlock textBlock)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("![") || !trimmed.Contains("](")) return false;

        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"!\[(.*?)\]\((.*?)\)");
        if (!match.Success) return false;

        var alt = match.Groups[1].Value;
        var url = match.Groups[2].Value;

        try
        {
            var uri = new Uri(url);
            var img = new Image
            {
                Source = new BitmapImage(uri),
                MaxHeight = 300,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 4, 0, 4)
            };
            ToolTipService.SetToolTip(img, alt);

            var container = new InlineUIContainer { Child = img };
            textBlock.Inlines.Add(container);
            textBlock.Inlines.Add(new LineBreak());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ParseInlineLine(string line, TextBlock textBlock, bool addNewline)
    {
        var span = CreateInlineSpan(textBlock, line);
        foreach (var inline in span.Inlines)
        {
            textBlock.Inlines.Add(inline);
        }
        if (addNewline) textBlock.Inlines.Add(new LineBreak());
    }

    private static Span CreateInlineSpan(TextBlock textBlock, string text)
    {
        var span = new Span();
        if (string.IsNullOrEmpty(text)) return span;

        var pattern = @"(\*\*\*.+?\*\*\*|\*\*.+?\*\*|\*.+?\*|~~.+?~~|`[^`]+`|!\[[^\]]*\]\([^)]*\)|\[[^\]]*\]\([^)]*\)|<br\s*/?>)";
        var segments = System.Text.RegularExpressions.Regex.Split(text, pattern);

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment)) continue;

            if (segment.StartsWith("***") && segment.EndsWith("***") && segment.Length > 6)
            {
                var inner = segment.Substring(3, segment.Length - 6);
                span.Inlines.Add(new Bold { Inlines = { new Italic { Inlines = { new Run { Text = inner } } } } });
            }
            else if (segment.StartsWith("**") && segment.EndsWith("**") && segment.Length > 4)
            {
                span.Inlines.Add(new Bold { Inlines = { new Run { Text = segment.Substring(2, segment.Length - 4) } } });
            }
            else if (segment.StartsWith("*") && segment.EndsWith("*") && segment.Length > 2 && !segment.StartsWith("**"))
            {
                span.Inlines.Add(new Italic { Inlines = { new Run { Text = segment.Substring(1, segment.Length - 2) } } });
            }
            else if (segment.StartsWith("~~") && segment.EndsWith("~~") && segment.Length > 4)
            {
                var run = new Run { Text = segment.Substring(2, segment.Length - 4) };
                var strikeSpan = new Span { Inlines = { run }, TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough };
                span.Inlines.Add(strikeSpan);
            }
            else if (segment.StartsWith("`") && segment.EndsWith("`") && segment.Length > 2)
            {
                span.Inlines.Add(CreateCodeRun(textBlock, segment.Substring(1, segment.Length - 2)));
            }
            else if (segment.StartsWith("![") && segment.Contains("]("))
            {
                var match = System.Text.RegularExpressions.Regex.Match(segment, @"!\[(.*?)\]\((.*?)\)");
                if (match.Success)
                {
                    var alt = match.Groups[1].Value;
                    var url = match.Groups[2].Value;
                    try
                    {
                        var img = new Image
                        {
                            Source = new BitmapImage(new Uri(url)),
                            MaxHeight = 200,
                            Stretch = Stretch.Uniform
                        };
                        ToolTipService.SetToolTip(img, alt);
                        span.Inlines.Add(new InlineUIContainer { Child = img });
                    }
                    catch { span.Inlines.Add(new Run { Text = segment }); }
                }
                else { span.Inlines.Add(new Run { Text = segment }); }
            }
            else if (segment.StartsWith("[") && segment.Contains("]("))
            {
                var match = System.Text.RegularExpressions.Regex.Match(segment, @"\[(.*?)\]\((.*?)\)");
                if (match.Success)
                {
                    var linkText = match.Groups[1].Value;
                    var url = match.Groups[2].Value;
                    try
                    {
                        var hyperlink = new Hyperlink { NavigateUri = new Uri(url) };
                        hyperlink.Inlines.Add(new Run { Text = linkText });
                        ToolTipService.SetToolTip(hyperlink, url);
                        span.Inlines.Add(hyperlink);
                    }
                    catch { span.Inlines.Add(new Run { Text = segment }); }
                }
                else { span.Inlines.Add(new Run { Text = segment }); }
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(segment, @"^<br\s*/?>$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                span.Inlines.Add(new LineBreak());
            }
            else
            {
                span.Inlines.Add(new Run { Text = segment });
            }
        }

        return span;
    }

    private static Run CreateCodeRun(TextBlock textBlock, string code)
    {
        return new Run
        {
            Text = code,
            FontFamily = new FontFamily("Consolas"),
            Foreground = BrushCache.AccentBrush,
            FontSize = Math.Max(10, textBlock.FontSize - 1)
        };
    }

    private static void AddCodeBlock(TextBlock textBlock, string code, string? lang)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(20, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var stack = new StackPanel();

        if (!string.IsNullOrEmpty(lang))
        {
            stack.Children.Add(new TextBlock
            {
                Text = lang,
                FontSize = 10,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                Margin = new Thickness(0, 0, 0, 4)
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        });

        border.Child = stack;
        var container = new InlineUIContainer { Child = border };
        textBlock.Inlines.Add(container);
        textBlock.Inlines.Add(new LineBreak());
    }
}

internal static class BrushCache
{
    private static Brush? _accentBrush;
    private static readonly object _lock = new();

    public static Brush AccentBrush
    {
        get
        {
            if (_accentBrush == null)
            {
                lock (_lock)
                {
                    if (_accentBrush == null)
                    {
                        try
                        {
                            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var res))
                            {
                                _accentBrush = res is Windows.UI.Color color
                                    ? new SolidColorBrush(color)
                                    : res as Brush;
                            }
                        }
                        catch { }
                        _accentBrush ??= new SolidColorBrush(Microsoft.UI.Colors.Blue);
                    }
                }
            }
            return _accentBrush;
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
