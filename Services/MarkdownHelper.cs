using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using System.Text;
using Microsoft.UI.Text;
using Windows.UI.Text;

namespace JulesClient.Services;

internal static class MdStyles
{
    public static FontWeight Bold => Microsoft.UI.Text.FontWeights.Bold;
    public static FontWeight SemiBold => Microsoft.UI.Text.FontWeights.SemiBold;
    public static FontWeight Normal => Microsoft.UI.Text.FontWeights.Normal;
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
            try { textBlock.Inlines.Clear(); } catch { }
            textBlock.Text = text;
        }
    }

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static bool TryParseCodeBlock(string[] lines, ref int index, TextBlock textBlock)
    {
        try
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

            if (!string.IsNullOrEmpty(lang))
            {
                var langRun = new Run { Text = $"// {lang}", FontSize = 10, Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) };
                textBlock.Inlines.Add(langRun);
                textBlock.Inlines.Add(new LineBreak());
            }

            var codeRun = new Run { Text = sb.ToString(), FontFamily = new FontFamily("Consolas"), FontSize = 12 };
            textBlock.Inlines.Add(codeRun);
            textBlock.Inlines.Add(new LineBreak());
            return true;
        }
        catch { return false; }
    }

    private static bool TryParseHeading(string line, TextBlock textBlock)
    {
        try
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
            }
            textBlock.Inlines.Add(span);
            textBlock.Inlines.Add(new LineBreak());
            return true;
        }
        catch { return false; }
    }

    private static bool TryParseHorizontalRule(string line, TextBlock textBlock)
    {
        try
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 3) return false;

            char marker = trimmed[0];
            if (marker != '-' && marker != '*' && marker != '_') return false;

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] != marker && trimmed[i] != ' ') return false;
            }

            // FIXED: Render a true, dynamic full-width rule line instead of 10 static dashes
            var lineRect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                Opacity = 0.4,
                Margin = new Thickness(0, 8, 0, 8)
            };
            var container = new InlineUIContainer { Child = lineRect };
            textBlock.Inlines.Add(container);
            textBlock.Inlines.Add(new LineBreak());
            return true;
        }
        catch { return false; }
    }

    private static bool TryParseBlockquote(string[] lines, ref int index, TextBlock textBlock)
    {
        try
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

            // FIXED: Render blockquotes as Italicized and slightly muted for better visual structure
            var quoteSpan = CreateInlineSpan(textBlock, sb.ToString());
            quoteSpan.FontStyle = FontStyle.Italic;
            quoteSpan.Foreground = new SolidColorBrush(Microsoft.UI.Colors.DimGray);

            textBlock.Inlines.Add(quoteSpan);
            textBlock.Inlines.Add(new LineBreak());
            return true;
        }
        catch { return false; }
    }

    private static bool TryParseUnorderedList(string[] lines, ref int index, TextBlock textBlock)
    {
        try
        {
            var line = lines[index];
            if (!IsUnorderedListItem(line)) return false;

            // FIXED: Removed the constraint forcing listCount >= 2. Lists of size 1 are fully valid.
            while (index < lines.Length)
            {
                var current = lines[index];
                if (!IsUnorderedListItem(current)) break;

                var content = ExtractListItemContent(current);
                var bulletRun = new Run { Text = "\u2022  ", FontWeight = MdStyles.Bold };
                var contentSpan = CreateInlineSpan(textBlock, content);
                var containerSpan = new Span();
                containerSpan.Inlines.Add(bulletRun);
                containerSpan.Inlines.Add(contentSpan);
                textBlock.Inlines.Add(containerSpan);
                textBlock.Inlines.Add(new LineBreak());
                index++;
            }
            return true;
        }
        catch { return false; }
    }

    private static bool IsUnorderedListItem(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length < 2) return false;
        if (!trimmed.StartsWith("- ") && !trimmed.StartsWith("* ") && !trimmed.StartsWith("+ ")) return false;
        return true;
    }

    private static string ExtractListItemContent(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.Substring(2).Trim();
    }

    private static bool TryParseOrderedList(string[] lines, ref int index, TextBlock textBlock)
    {
        try
        {
            var line = lines[index];
            if (!IsOrderedListItem(line)) return false;

            int itemNum = 1;
            while (index < lines.Length)
            {
                var current = lines[index];
                if (!IsOrderedListItem(current)) break;

                var content = ExtractOrderedItemContent(current);
                var numRun = new Run { Text = $"{itemNum}.  ", FontWeight = MdStyles.SemiBold };
                var contentSpan = CreateInlineSpan(textBlock, content);
                var containerSpan = new Span();
                containerSpan.Inlines.Add(numRun);
                containerSpan.Inlines.Add(contentSpan);
                textBlock.Inlines.Add(containerSpan);
                textBlock.Inlines.Add(new LineBreak());
                itemNum++;
                index++;
            }
            return true;
        }
        catch { return false; }
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
        try
        {
            // FIXED: Keep state local and only commit to 'index = localIndex' when return is guaranteed true.
            var localIndex = index;
            var line = lines[localIndex];
            if (!line.Contains('|')) return false;

            var rows = new List<string[]>();
            var headerParts = ParseTableRow(line);
            if (headerParts.Length < 2) return false;

            rows.Add(headerParts);

            localIndex++;
            if (localIndex >= lines.Length) return false;

            var sepLine = lines[localIndex].Trim();
            if (!sepLine.Contains('|') || !sepLine.Contains('-')) return false;
            localIndex++;

            while (localIndex < lines.Length)
            {
                var current = lines[localIndex].Trim();
                if (!current.Contains('|')) break;
                var parts = ParseTableRow(current);
                if (parts.Length != headerParts.Length) break;
                rows.Add(parts);
                localIndex++;
            }

            var colCount = headerParts.Length;
            var colWidths = new int[colCount];
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    var cellContent = c < rows[r].Length ? rows[r][c].Trim() : "";
                    if (cellContent.Length > colWidths[c]) colWidths[c] = cellContent.Length;
                }
            }

            var sb = new StringBuilder();
            for (int r = 0; r < rows.Count; r++)
            {
                var lineSb = new StringBuilder();
                for (int c = 0; c < colCount; c++)
                {
                    var cellContent = c < rows[r].Length ? rows[r][c].Trim() : "";

                    // FIXED: Properly truncate long cells to prevent misalignment due to PadRight limitations
                    if (cellContent.Length > 18)
                    {
                        cellContent = cellContent.Substring(0, 15) + "...";
                    }

                    lineSb.Append(cellContent.PadRight(Math.Min(colWidths[c] + 2, 20)));
                }
                sb.AppendLine(lineSb.ToString());
                if (r == 0)
                {
                    var sepSb = new StringBuilder();
                    for (int c = 0; c < colCount; c++)
                    {
                        sepSb.Append(new string('-', Math.Min(colWidths[c] + 2, 20)));
                    }
                    sb.AppendLine(sepSb.ToString());
                }
            }

            var tableRun = new Run { Text = sb.ToString(), FontFamily = new FontFamily("Consolas"), FontSize = 11 };
            textBlock.Inlines.Add(tableRun);
            textBlock.Inlines.Add(new LineBreak());

            // Commit final index shift on success
            index = localIndex;
            return true;
        }
        catch { return false; }
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
        try
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("![") || !trimmed.Contains("](")) return false;

            var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"!\[(.*?)\]\((.*?)\)");
            if (!match.Success) return false;

            var alt = match.Groups[1].Value;

            var imgRun = new Run { Text = $"[Image: {alt}]", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray), FontSize = 12 };
            textBlock.Inlines.Add(imgRun);
            textBlock.Inlines.Add(new LineBreak());
            return true;
        }
        catch { return false; }
    }

    private static void ParseInlineLine(string line, TextBlock textBlock, bool addNewline)
    {
        try
        {
            var span = CreateInlineSpan(textBlock, line);
            textBlock.Inlines.Add(span);
            if (addNewline) textBlock.Inlines.Add(new LineBreak());
        }
        catch
        {
            textBlock.Inlines.Add(new Run { Text = line });
            if (addNewline) textBlock.Inlines.Add(new LineBreak());
        }
    }

    private static Span CreateInlineSpan(TextBlock textBlock, string text)
    {
        var span = new Span();
        if (string.IsNullOrEmpty(text)) return span;

        var pattern = @"(\*\*\*.+?\*\*\*|\*\*.+?\*\*|\*.+?\*|~~.+?~~|`[^`]+`|!\[[^\]]*\]\([^)]*\)|\[[^\]]*\]\([^)]*\)|<br\s*/?>)";
        var segments = System.Text.RegularExpressions.Regex.Split(text, pattern);

        foreach (var segment in segments)
        {
            try
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
                    span.Inlines.Add(new Run { Text = segment.Substring(2, segment.Length - 4), Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) });
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
                        span.Inlines.Add(new Run { Text = $"[Image: {alt}]", Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) });
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

                        // FIXED: Rendered proper clickable WinUI 3 Hyperlinks instead of plain blue Runs
                        try
                        {
                            var hyperlink = new Hyperlink();
                            if (Uri.TryCreate(url, UriKind.Absolute, out var validatedUri))
                            {
                                hyperlink.NavigateUri = validatedUri;
                            }
                            hyperlink.Inlines.Add(new Run { Text = linkText });
                            span.Inlines.Add(hyperlink);
                        }
                        catch
                        {
                            span.Inlines.Add(new Run { Text = linkText, Foreground = new SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue) });
                        }
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[MARKDOWN] Inline parse failed for segment: {ex.Message}");
                try { span.Inlines.Add(new Run { Text = segment }); } catch { }
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
}

internal static class BrushCache
{
    // FIXED: Removed permanent caching so that dynamically updated accent colors and dark/light mode switches take effect immediately.
    public static Brush AccentBrush
    {
        get
        {
            try
            {
                if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var res))
                {
                    if (res is Windows.UI.Color color)
                    {
                        return new SolidColorBrush(color);
                    }
                    if (res is Brush brush)
                    {
                        return brush;
                    }
                }
            }
            catch { }
            return new SolidColorBrush(Microsoft.UI.Colors.Blue);
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
        if (d is TextBlock tb)
        {
            // FIXED: Coalesce null values to string.Empty so that clearing text correctly triggers ParseInto and clears the TextBlock.Inlines
            var text = e.NewValue as string ?? string.Empty;

            if (tb.DispatcherQueue?.HasThreadAccess == true)
            {
                MarkdownParser.ParseInto(tb, text);
            }
            else
            {
                tb.DispatcherQueue?.TryEnqueue(() => MarkdownParser.ParseInto(tb, text));
            }
        }
    }
}