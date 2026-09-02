using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace JulesClient.Services;

public enum MarkdownInlineKind
{
    Text,
    Bold,
    Italic,
    BoldItalic,
    Code,
    Strikethrough,
    Link,
    Image,
    LineBreak,
    /// <summary>A review verdict marker, "#Correct#" / "#Partially Correct#" / …
    /// <see cref="MarkdownInlineToken.Text"/> is the label without the '#' fences.</summary>
    Verdict
}

/// <summary>One piece of a single rendered line. <see cref="Text"/> is the display
/// text (link text / image alt for those kinds); <see cref="Url"/> is set for
/// <see cref="MarkdownInlineKind.Link"/> and <see cref="MarkdownInlineKind.Image"/>.</summary>
public sealed record MarkdownInlineToken(MarkdownInlineKind Kind, string Text, string? Url = null);

/// <summary>
/// Pure (UI-free) tokenizer for the inline span of a single Markdown line.
/// Kept separate from the WinUI rendering so it can be unit tested. Block
/// structure (headings, lists, code fences, tables) is handled by
/// <c>MarkdownParser</c>; this only sees the already-split line content.
/// </summary>
public static class MarkdownInline
{
    // Any ASCII punctuation may be backslash-escaped, per CommonMark.
    private const string EscapablePunctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

    // \G anchors the match at the start index passed to Regex.Match.
    private static readonly Regex BrTag = new(@"\G<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AngleAutolink = new(@"\G<((?:https?|ftp)://[^>\s]+)>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BareUrl = new(@"\Ghttps?://[^\s<>()\[\]]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // Jules review ratings, lower-cased and single-spaced. Only these render as a
    // verdict; anything else in "#...#" shape stays literal (so "#123", "#FF0000",
    // "#include", issue refs like "#42" are untouched).
    private static readonly HashSet<string> Verdicts = new(System.StringComparer.Ordinal)
    {
        "correct", "partially correct", "mostly correct",
        "incorrect", "partially incorrect", "wrong"
    };

    public static IReadOnlyList<MarkdownInlineToken> Tokenize(string? text)
    {
        var tokens = new List<MarkdownInlineToken>();
        if (string.IsNullOrEmpty(text)) return tokens;

        try
        {
            var buf = new StringBuilder();
            int i = 0;
            int n = text.Length;

            void Flush()
            {
                if (buf.Length > 0)
                {
                    tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Text, buf.ToString()));
                    buf.Clear();
                }
            }

            while (i < n)
            {
                char c = text[i];

                // Backslash escape: the next punctuation char is taken literally.
                if (c == '\\' && i + 1 < n && EscapablePunctuation.IndexOf(text[i + 1]) >= 0)
                {
                    buf.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '<')
                {
                    var br = BrTag.Match(text, i);
                    if (br.Success && br.Index == i)
                    {
                        Flush();
                        tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.LineBreak, string.Empty));
                        i += br.Length;
                        continue;
                    }

                    var auto = AngleAutolink.Match(text, i);
                    if (auto.Success && auto.Index == i)
                    {
                        Flush();
                        var u = auto.Groups[1].Value;
                        tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Link, u, u));
                        i += auto.Length;
                        continue;
                    }
                }

                // Image: ![alt](url)
                if (c == '!' && i + 1 < n && text[i + 1] == '[' &&
                    TryLink(text, i + 1, out var alt, out var imgUrl, out var imgConsumed))
                {
                    Flush();
                    tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Image, alt, imgUrl));
                    i += 1 + imgConsumed;
                    continue;
                }

                // Link: [text](url)
                if (c == '[' && TryLink(text, i, out var linkText, out var linkUrl, out var linkConsumed))
                {
                    Flush();
                    tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Link, linkText, linkUrl));
                    i += linkConsumed;
                    continue;
                }

                // Code span: `code` or ``code with ` inside``
                if (c == '`')
                {
                    int ticks = CountRun(text, i, '`');
                    int close = IndexOfRun(text, i + ticks, '`', ticks);
                    if (close >= 0)
                    {
                        Flush();
                        var code = text.Substring(i + ticks, close - (i + ticks));
                        code = code.Trim();
                        tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Code, code));
                        i = close + ticks;
                        continue;
                    }
                }

                // Emphasis: *, **, *** and _, __, ___
                if (c == '*' || c == '_')
                {
                    int run = CountRun(text, i, c);
                    int markerLen = run >= 3 ? 3 : run;
                    if (TryEmphasis(text, i, c, markerLen, out var inner, out var end))
                    {
                        Flush();
                        var kind = markerLen switch
                        {
                            3 => MarkdownInlineKind.BoldItalic,
                            2 => MarkdownInlineKind.Bold,
                            _ => MarkdownInlineKind.Italic
                        };
                        tokens.Add(new MarkdownInlineToken(kind, inner));
                        i = end;
                        continue;
                    }
                }

                // Review verdict marker: #Correct#, #Partially Correct#, ...
                if (c == '#' && (i == 0 || !IsVerdictBoundaryChar(text[i - 1])) &&
                    i + 1 < n && char.IsLetter(text[i + 1]))
                {
                    int k = i + 1;
                    while (k < n && (char.IsLetter(text[k]) || text[k] == ' ')) k++;
                    if (k < n && text[k] == '#' && k - (i + 1) <= 24)
                    {
                        var label = text.Substring(i + 1, k - (i + 1)).Trim();
                        var norm = Whitespace.Replace(label, " ").ToLowerInvariant();
                        if (label.Length > 0 && Verdicts.Contains(norm))
                        {
                            Flush();
                            tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Verdict, label));
                            i = k + 1;
                            continue;
                        }
                    }
                }

                // Strikethrough: ~~text~~
                if (c == '~' && i + 1 < n && text[i + 1] == '~')
                {
                    int close = text.IndexOf("~~", i + 2, System.StringComparison.Ordinal);
                    if (close > i + 2)
                    {
                        Flush();
                        tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Strikethrough, text.Substring(i + 2, close - (i + 2))));
                        i = close + 2;
                        continue;
                    }
                }

                // Bare autolink: http://... / https://... at a word boundary
                if ((c == 'h' || c == 'H') && (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
                {
                    var m = BareUrl.Match(text, i);
                    if (m.Success && m.Index == i)
                    {
                        var url = m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '"', '\'');
                        if (url.Length > "https://".Length)
                        {
                            Flush();
                            tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Link, url, url));
                            i += url.Length;
                            continue;
                        }
                    }
                }

                buf.Append(c);
                i++;
            }

            Flush();
        }
        catch
        {
            tokens.Clear();
            tokens.Add(new MarkdownInlineToken(MarkdownInlineKind.Text, text));
        }

        return tokens;
    }

    // A "#verdict#" must stand alone, not be glued to a word or another '#'.
    private static bool IsVerdictBoundaryChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '#';

    private static int CountRun(string s, int idx, char ch)
    {
        int k = idx;
        while (k < s.Length && s[k] == ch) k++;
        return k - idx;
    }

    // First index at or after <from> where a run of <ch> of length >= <len> starts.
    private static int IndexOfRun(string s, int from, char ch, int len)
    {
        for (int k = from; k < s.Length; k++)
        {
            if (s[k] != ch) continue;
            if (CountRun(s, k, ch) >= len) return k;
        }
        return -1;
    }

    private static bool TryEmphasis(string s, int start, char ch, int markerLen, out string inner, out int end)
    {
        inner = string.Empty;
        end = 0;

        int contentStart = start + markerLen;
        if (contentStart >= s.Length || char.IsWhiteSpace(s[contentStart])) return false;
        // "_" does not open emphasis inside a word (snake_case stays intact).
        if (ch == '_' && start > 0 && char.IsLetterOrDigit(s[start - 1])) return false;

        int j = contentStart;
        while (j < s.Length)
        {
            char cur = s[j];
            if (cur == '\\') { j += 2; continue; }

            if (cur == ch)
            {
                int run = CountRun(s, j, ch);
                if (run >= markerLen && !char.IsWhiteSpace(s[j - 1]))
                {
                    int after = j + markerLen;
                    if (ch == '_' && after < s.Length && char.IsLetterOrDigit(s[after]))
                    {
                        j += run;
                        continue;
                    }

                    var content = s.Substring(contentStart, j - contentStart);
                    if (content.Trim().Length == 0) return false;

                    inner = content;
                    end = j + markerLen;
                    return true;
                }

                j += run;
                continue;
            }

            j++;
        }

        return false;
    }

    // Parses "[text](url ...)" starting at s[start] == '['. Returns the number of
    // chars consumed (through the closing ')') in <consumed>.
    private static bool TryLink(string s, int start, out string linkText, out string url, out int consumed)
    {
        linkText = string.Empty;
        url = string.Empty;
        consumed = 0;

        if (start >= s.Length || s[start] != '[') return false;

        var textSb = new StringBuilder();
        int i = start + 1;
        bool closedBracket = false;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length) { textSb.Append(s[i + 1]); i++; continue; }
            if (c == '\n') return false;
            if (c == ']') { closedBracket = true; i++; break; }
            textSb.Append(c);
        }
        if (!closedBracket || i >= s.Length || s[i] != '(') return false;
        i++; // past '('

        var urlSb = new StringBuilder();
        bool closedParen = false;
        int depth = 0;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length) { urlSb.Append(s[i + 1]); i++; continue; }
            if (c == '\n') return false;
            if (c == '(') { depth++; urlSb.Append(c); continue; }
            if (c == ')')
            {
                if (depth == 0) { closedParen = true; i++; break; }
                depth--;
                urlSb.Append(c);
                continue;
            }
            urlSb.Append(c);
        }
        if (!closedParen) return false;

        var raw = urlSb.ToString().Trim();
        // Drop an optional link title: (url "title") / (url 'title').
        int sp = raw.IndexOf(' ');
        if (sp > 0)
        {
            var rest = raw.Substring(sp + 1).TrimStart();
            if (rest.StartsWith("\"") || rest.StartsWith("'") || rest.StartsWith("("))
                raw = raw.Substring(0, sp);
        }
        if (raw.Length >= 2 && raw[0] == '<' && raw[^1] == '>')
            raw = raw.Substring(1, raw.Length - 2);

        linkText = textSb.ToString();
        url = raw.Trim();
        consumed = i - start;
        return true;
    }
}
