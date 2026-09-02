using Microsoft.UI;
using FontWeight = Windows.UI.Text.FontWeight;
using FontWeights = Microsoft.UI.Text.FontWeights;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Text;

namespace JulesClient.Services;

internal static class MdStyles
{
    public static FontWeight Bold => FontWeights.Bold;
    public static FontWeight SemiBold => FontWeights.SemiBold;
    public static FontWeight Normal => FontWeights.Normal;
}

public static class MarkdownParser
{
    public static void ParseInto(TextBlock textBlock, string text)
    {
        try
        {
            textBlock.Inlines.Clear();
            if (string.IsNullOrEmpty(text)) return;

            var lines = MarkdownText.DedupeRepeatedLabels(
                text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
            var i = 0;
            var pendingParagraphBreak = false;

            while (i < lines.Length)
            {
                var line = lines[i];

                if (IsBlank(line))
                {
                    // Remember that a blank line separated two blocks; emit the
                    // extra spacing only once, and only between real content.
                    if (textBlock.Inlines.Count > 0) pendingParagraphBreak = true;
                    i++;
                    continue;
                }

                if (pendingParagraphBreak)
                {
                    textBlock.Inlines.Add(new LineBreak());
                    pendingParagraphBreak = false;
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

            // Every content line adds a trailing LineBreak; drop the last one(s)
            // so a message doesn't render an empty line of whitespace below it.
            while (textBlock.Inlines.Count > 0 && textBlock.Inlines[^1] is LineBreak)
            {
                textBlock.Inlines.RemoveAt(textBlock.Inlines.Count - 1);
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
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(lines[index]);
                index++;
            }

            AppendCodeBlock(textBlock, sb.ToString(), lang);
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

            // ATX headings require whitespace (or end of line) after the '#' run,
            // so "#hashtag" and a bare "C#" stay as ordinary text.
            if (trimmed.Length > level && trimmed[level] != ' ' && trimmed[level] != '\t') return false;

            var content = trimmed.Substring(level).Trim();
            if (content.Length == 0) return false;

            // Strip an optional closing '#' sequence, but only when it is
            // space-separated (keeps a trailing "#" that is part of the text).
            int endTrim = content.Length;
            while (endTrim > 0 && content[endTrim - 1] == '#') endTrim--;
            if (endTrim < content.Length && (endTrim == 0 || content[endTrim - 1] == ' '))
                content = content.Substring(0, endTrim).TrimEnd();
            if (content.Length == 0) return false;

            double fontSize = level switch
            {
                1 => 30,
                2 => 24,
                3 => 20,
                4 => 17,
                5 => 15,
                _ => 14
            };

            var weight = level <= 2 ? MdStyles.Bold : MdStyles.SemiBold;

            // Breathing room above a heading (unless it's the first thing, or a
            // paragraph break already put a gap there).
            if (textBlock.Inlines.Count > 0 && textBlock.Inlines[^1] is not LineBreak)
                textBlock.Inlines.Add(new LineBreak());

            // Size/weight on the span cascade to every child inline, so inline
            // formatting inside a heading (e.g. `**bold**`, code, links) keeps the
            // heading's scale.
            var span = CreateInlineSpan(textBlock, content);
            span.FontSize = fontSize;
            span.FontWeight = weight;
            if (level <= 2) span.CharacterSpacing = -10; // tighten large display sizes
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

            if (textBlock.Inlines.Count > 0 && textBlock.Inlines[^1] is not LineBreak)
                textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run
            {
                Text = new string('\u2500', 40),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.5 },
                FontSize = 12
            });
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

            var quoteBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);
            bool any = false;
            while (index < lines.Length)
            {
                var current = lines[index].TrimStart();
                if (!current.StartsWith(">")) break;

                var content = current.Substring(1);
                if (content.StartsWith(" ")) content = content.Substring(1);

                // An accent left bar + muted italic reads as a quote instead of a
                // plain paragraph; each wrapped source line keeps its own bar.
                var row = new Span { Foreground = quoteBrush };
                row.Inlines.Add(new Run { Text = "┃  ", Foreground = BrushCache.AccentBrush });
                var italic = new Italic();
                italic.Inlines.Add(CreateInlineSpan(textBlock, content));
                row.Inlines.Add(italic);
                textBlock.Inlines.Add(row);
                textBlock.Inlines.Add(new LineBreak());
                any = true;
                index++;
            }

            return any;
        }
        catch { return false; }
    }

    private static bool TryParseUnorderedList(string[] lines, ref int index, TextBlock textBlock)
    {
        try
        {
            var line = lines[index];
            if (!IsUnorderedListItem(line)) return false;

            while (index < lines.Length)
            {
                var current = lines[index];
                if (!IsUnorderedListItem(current)) break;

                var content = ExtractListItemContent(current);

                // GitHub-style task list items: "- [ ] todo" / "- [x] done".
                string bullet = "\u2022  ";
                if (content.StartsWith("[ ] "))
                {
                    bullet = "\u2610  ";
                    content = content.Substring(4);
                }
                else if (content.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase))
                {
                    bullet = "\u2611  ";
                    content = content.Substring(4);
                }

                var bulletRun = new Run { Text = bullet, FontWeight = MdStyles.Bold };
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
        if (trimmed.Length < 3) return false;
        if (!trimmed.StartsWith("- ") && !trimmed.StartsWith("* ") && !trimmed.StartsWith("+ ")) return false;
        var afterMarker = trimmed.Substring(2);
        if (string.IsNullOrWhiteSpace(afterMarker)) return false;
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
            if (!TryGetOrderedItem(lines[index], out var startNumber, out _)) return false;

            // Honour the number written in the source. Agent output numbers each
            // point 1., 2., 3. but separates them with blank lines / sub-bullets,
            // so each lands here as its own one-item list - a synthetic counter
            // would restart at 1 every time and label them all "1.".
            int displayNumber = startNumber;
            bool first = true;

            while (index < lines.Length && TryGetOrderedItem(lines[index], out var sourceNumber, out var content))
            {
                if (!first)
                    displayNumber = sourceNumber > displayNumber ? sourceNumber : displayNumber + 1;
                first = false;

                var containerSpan = new Span();
                containerSpan.Inlines.Add(new Run { Text = $"{displayNumber}.  ", FontWeight = MdStyles.SemiBold });
                containerSpan.Inlines.Add(CreateInlineSpan(textBlock, content));
                textBlock.Inlines.Add(containerSpan);
                textBlock.Inlines.Add(new LineBreak());
                index++;
            }
            return true;
        }
        catch { return false; }
    }

    private static bool TryGetOrderedItem(string line, out int number, out string content)
    {
        number = 0;
        content = string.Empty;

        var trimmed = line.TrimStart();
        var dotIdx = trimmed.IndexOf(". ", StringComparison.Ordinal);
        if (dotIdx <= 0 || !int.TryParse(trimmed.Substring(0, dotIdx), out number)) return false;

        content = trimmed.Substring(dotIdx + 2).Trim();
        return true;
    }

    private static bool TryParseTable(string[] lines, ref int index, TextBlock textBlock)
    {
        try
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
                    // Last column isn't padded, so wide cells are no longer clipped.
                    lineSb.Append(c == colCount - 1 ? cellContent : cellContent.PadRight(colWidths[c] + 2));
                }
                sb.AppendLine(lineSb.ToString());
                if (r == 0)
                {
                    var sepSb = new StringBuilder();
                    for (int c = 0; c < colCount; c++)
                    {
                        sepSb.Append(new string('-', colWidths[c] + 2));
                    }
                    sb.AppendLine(sepSb.ToString());
                }
            }

            var tableRun = new Run { Text = sb.ToString(), FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11 };
            textBlock.Inlines.Add(tableRun);
            textBlock.Inlines.Add(new LineBreak());
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
            var url = match.Groups[2].Value;

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

        foreach (var token in MarkdownInline.Tokenize(text))
        {
            try
            {
                span.Inlines.Add(ToInline(textBlock, token));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MARKDOWN] Inline render failed: {ex.Message}");
                try { span.Inlines.Add(new Run { Text = token.Text }); } catch { }
            }
        }

        return span;
    }

    private static Inline ToInline(TextBlock textBlock, MarkdownInlineToken token)
    {
        switch (token.Kind)
        {
            case MarkdownInlineKind.Bold:
                return new Bold { Inlines = { new Run { Text = token.Text } } };
            case MarkdownInlineKind.Italic:
                return new Italic { Inlines = { new Run { Text = token.Text } } };
            case MarkdownInlineKind.BoldItalic:
                return new Bold { Inlines = { new Italic { Inlines = { new Run { Text = token.Text } } } } };
            case MarkdownInlineKind.Strikethrough:
                return new Run { Text = token.Text, TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough };
            case MarkdownInlineKind.Code:
                return CreateCodeRun(textBlock, token.Text);
            case MarkdownInlineKind.Image:
                return new Run
                {
                    Text = string.IsNullOrEmpty(token.Text) ? "[Image]" : $"[Image: {token.Text}]",
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                };
            case MarkdownInlineKind.LineBreak:
                return new LineBreak();
            case MarkdownInlineKind.Link:
                return CreateLink(token);
            case MarkdownInlineKind.Verdict:
                return CreateVerdict(token.Text);
            default:
                return new Run { Text = token.Text };
        }
    }

    // Review rating markers ("#Correct#" etc.) -> a glyph plus the label in bold
    // coloured caps. A real pill/background box isn't possible in a TextBlock.
    private static Inline CreateVerdict(string label)
    {
        var lower = label.ToLowerInvariant();

        string glyph;
        SolidColorBrush brush;
        if (lower.Contains("partial") || lower.Contains("mostly"))
        {
            glyph = "▲"; // ▲
            brush = SolidHex(IsDarkTheme() ? "#E3B341" : "#9A6700");
        }
        else if (lower.Contains("incorrect") || lower.Contains("wrong"))
        {
            glyph = "✗"; // ✗
            brush = SolidHex(IsDarkTheme() ? "#FF7B72" : "#CF222E");
        }
        else
        {
            glyph = "✓"; // ✓
            brush = SolidHex(IsDarkTheme() ? "#3FB950" : "#1A7F37");
        }

        var bold = new Bold { Foreground = brush };
        bold.Inlines.Add(new Run { Text = $"{glyph} {label.ToUpperInvariant()}" });
        return bold;
    }

    private static Inline CreateLink(MarkdownInlineToken token)
    {
        var display = string.IsNullOrEmpty(token.Text) ? (token.Url ?? string.Empty) : token.Text;

        if (Uri.TryCreate(token.Url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var link = new Hyperlink { NavigateUri = uri };
            link.Inlines.Add(new Run { Text = display });
            return link;
        }

        return new Run { Text = display, Foreground = new SolidColorBrush(Microsoft.UI.Colors.CornflowerBlue) };
    }

    private static Run CreateCodeRun(TextBlock textBlock, string code)
    {
        return new Run
        {
            Text = code,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Foreground = BrushCache.AccentBrush,
            FontSize = Math.Max(10, textBlock.FontSize - 1)
        };
    }

    internal static Microsoft.UI.Xaml.Media.FontFamily CodeFont => new(AppSettings.CodeFontFamily);

    internal static double CodeFontSize => AppSettings.CodeFontSize;

    // A fenced ``` block. TextBlock.Inlines can't host a real Border
    // (InlineUIContainer is RichTextBlock-only), so the block is framed with a
    // rule above/below and a continuous left gutter bar on every line, with the
    // contents lightly syntax-highlighted.
    private static void AppendCodeBlock(TextBlock textBlock, string code, string? lang)
    {
        var frameBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);

        Run Rule() => new()
        {
            Text = "────────────────────",
            Foreground = frameBrush,
            FontSize = 8
        };

        Run Gutter() => new()
        {
            Text = "▏  ",
            Foreground = frameBrush,
            FontFamily = CodeFont,
            FontSize = CodeFontSize
        };

        textBlock.Inlines.Add(Rule());
        textBlock.Inlines.Add(new LineBreak());

        if (!string.IsNullOrEmpty(lang) && AppSettings.ShowCodeLanguageLabel)
        {
            textBlock.Inlines.Add(new Run { Text = lang, Foreground = frameBrush, FontFamily = CodeFont, FontSize = 10 });
            textBlock.Inlines.Add(new LineBreak());
        }

        textBlock.Inlines.Add(Gutter());
        foreach (var token in CodeHighlighter.Highlight(code, lang))
        {
            var brush = CodeTokenBrush(token.Kind);
            var parts = token.Text.Split('\n');

            for (int p = 0; p < parts.Length; p++)
            {
                if (p > 0)
                {
                    textBlock.Inlines.Add(new LineBreak());
                    textBlock.Inlines.Add(Gutter());
                }
                if (parts[p].Length == 0) continue;

                var run = new Run { Text = parts[p], FontFamily = CodeFont, FontSize = CodeFontSize };
                if (brush != null) run.Foreground = brush;

                if (token.Kind == CodeTokenKind.Comment)
                    textBlock.Inlines.Add(new Italic { Inlines = { run } });
                else
                    textBlock.Inlines.Add(run);
            }
        }

        textBlock.Inlines.Add(new LineBreak());
        textBlock.Inlines.Add(Rule());
        textBlock.Inlines.Add(new LineBreak());
    }

    internal static bool IsDarkTheme()
    {
        try { return Application.Current.RequestedTheme == ApplicationTheme.Dark; }
        catch { return true; }
    }

    private static SolidColorBrush SolidHex(string hex)
    {
        byte r = Convert.ToByte(hex.Substring(1, 2), 16);
        byte g = Convert.ToByte(hex.Substring(3, 2), 16);
        byte b = Convert.ToByte(hex.Substring(5, 2), 16);
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0xFF, r, g, b));
    }

    internal static SolidColorBrush? CodeTokenBrush(CodeTokenKind kind)
    {
        if (!AppSettings.SyntaxHighlighting) return null;

        bool dark = IsDarkTheme();
        return kind switch
        {
            CodeTokenKind.Keyword => SolidHex(dark ? "#569CD6" : "#0000C0"),
            CodeTokenKind.Type => SolidHex(dark ? "#4EC9B0" : "#267F99"),
            CodeTokenKind.String => SolidHex(dark ? "#CE9178" : "#A31515"),
            CodeTokenKind.Comment => SolidHex(dark ? "#6A9955" : "#008000"),
            CodeTokenKind.Number => SolidHex(dark ? "#B5CEA8" : "#098658"),
            CodeTokenKind.Preprocessor => SolidHex(dark ? "#9B9B9B" : "#808080"),
            _ => null
        };
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
