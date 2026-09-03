using System;
using System.Collections.Generic;
using System.Text;

namespace JulesClient.Services;

/// <summary>A fenced code block lifted out of a Markdown string.</summary>
public sealed record FencedCodeBlock(string? Language, string Code);

/// <summary>One slice of Markdown: plain text to render when <see cref="Code"/>
/// is null, otherwise an extracted fenced code block.</summary>
public sealed record CodeSegment(string Text, FencedCodeBlock? Code);

/// <summary>
/// Splits Markdown into plain-text runs and closed fenced code blocks
/// (<c>```</c> or <c>~~~</c>). The presenter renders each block in its own
/// collapsible container instead of inline in a TextBlock. An unclosed fence is
/// left in the plain text for the inline renderer to handle.
/// </summary>
public static class FencedCode
{
    public static IReadOnlyList<CodeSegment> Split(string? text)
    {
        var result = new List<CodeSegment>();
        if (string.IsNullOrEmpty(text)) return result;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var buf = new List<string>();

        void FlushBuf()
        {
            if (buf.Count == 0) return;
            var joined = string.Join("\n", buf).Trim('\n');
            if (joined.Length > 0) result.Add(new CodeSegment(joined, null));
            buf.Clear();
        }

        int i = 0;
        while (i < lines.Length)
        {
            if (TryOpenFence(lines[i], out var fence, out var fenceLen, out var lang, out var indent))
            {
                int closeAt = -1;
                for (int j = i + 1; j < lines.Length; j++)
                {
                    if (IsCloseFence(lines[j], fence, fenceLen)) { closeAt = j; break; }
                }

                if (closeAt < 0)
                {
                    // No closing fence - hand the line back to the inline renderer.
                    buf.Add(lines[i]);
                    i++;
                    continue;
                }

                var body = new StringBuilder();
                for (int k = i + 1; k < closeAt; k++)
                {
                    if (body.Length > 0) body.Append('\n');
                    body.Append(StripIndent(lines[k], indent));
                }

                FlushBuf();
                result.Add(new CodeSegment(string.Empty, new FencedCodeBlock(
                    string.IsNullOrWhiteSpace(lang) ? null : lang, body.ToString())));
                i = closeAt + 1;
                continue;
            }

            buf.Add(lines[i]);
            i++;
        }

        FlushBuf();
        return result;
    }

    // A fence opener: >= 3 of '`' or '~', optionally indented, with an info
    // string whose first token is the language. A backtick info string may not
    // contain a backtick (CommonMark).
    internal static bool TryOpenFence(string line, out char fence, out int fenceLen, out string? lang, out int indent)
    {
        fence = '\0';
        fenceLen = 0;
        lang = null;
        indent = 0;

        var trimmed = line.TrimStart();
        if (trimmed.Length < 3) return false;

        var ch = trimmed[0];
        if (ch != '`' && ch != '~') return false;

        int len = 0;
        while (len < trimmed.Length && trimmed[len] == ch) len++;
        if (len < 3) return false;

        var info = trimmed.Substring(len).Trim();
        var first = info.Length == 0
            ? string.Empty
            : info.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
        if (ch == '`' && first.Contains('`')) return false;

        fence = ch;
        fenceLen = len;
        lang = first.Length == 0 ? null : first;
        indent = line.Length - trimmed.Length;
        return true;
    }

    // A closing fence is a run of >= minLen of the same fence char with nothing
    // else on the line (trailing whitespace allowed).
    internal static bool IsCloseFence(string line, char fence, int minLen)
    {
        var t = line.TrimStart().TrimEnd();
        if (t.Length < minLen || t[0] != fence) return false;

        int run = 0;
        while (run < t.Length && t[run] == fence) run++;
        return run == t.Length && run >= minLen;
    }

    // Drops up to <paramref name="count"/> leading spaces/tabs so a fence
    // indented inside a list item doesn't push its body text to the right.
    internal static string StripIndent(string line, int count)
    {
        int i = 0;
        while (i < line.Length && i < count && (line[i] == ' ' || line[i] == '\t')) i++;
        return i == 0 ? line : line.Substring(i);
    }
}
